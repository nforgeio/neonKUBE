//-----------------------------------------------------------------------------
// FILE:	    SshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;
using Renci.SshNet;
using Renci.SshNet.Common;

// $todo(jeff.lill):
//
// The download methods don't seem to be working for paths like [/proc/meminfo].
// They return an empty stream.

namespace Neon.Hive
{
    /// <summary>
    /// <para>
    /// Uses an SSH/SCP connection to provide access to Linux machines to access
    /// files, run commands, etc.
    /// </para>
    /// <note>
    /// This is class is <b>not intended</b> to be a <b>general purpose SSH wrapper</b> 
    /// at this time.  It currently assumes that the remote side is running some variant
    /// of Linux and it makes some globale changes including overwriting the 
    /// <b>/etc/sudoers.d/nopasswd</b> file to disable password prompts for all
    /// users and creating some global directories.
    /// </note>
    /// </summary>
    /// <typeparam name="TMetadata">
    /// Defines the metadata type the application wishes to associate with the server.
    /// You may specify <c>object</c> when no additional metadata is required.
    /// </typeparam>
    /// <remarks>
    /// <para>
    /// Construct an instance to connect to a specific hive node.  You may specify
    /// <typeparamref name="TMetadata"/> to associate application specific information
    /// or state with the instance.
    /// </para>
    /// <para>
    /// This class includes methods to invoke Linux commands on the node as well as
    /// methods to issue Docker commands against the local node or the Swarm hive.
    /// Methods are also provided to upload and download files.
    /// </para>
    /// <para>
    /// Call <see cref="Dispose()"/> or <see cref="Disconnect()"/> to close the connection.
    /// </para>
    /// <note>
    /// You can use <see cref="Clone()"/> to make a copy of a proxy that can be
    /// used to perform parallel operations against the same machine.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class SshProxy<TMetadata> : IDisposable
        where TMetadata : class
    {
        //---------------------------------------------------------------------
        // Static members

        private static Dictionary<string, object> connectLocks = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        private static Regex idempotentRegex = new Regex(@"[a-z0-9\.-/]+", RegexOptions.IgnoreCase);

        /// <summary>
        /// Returns the object to be used to when establishing connections to
        /// a target server.
        /// </summary>
        /// <param name="host">The target server hostname or IP address.</param>
        /// <returns>The lock object.</returns>
        private static object GetConnectLock(string host)
        {
            // $hack(jeff.lill):
            //
            // SSH.NET appears to have an issue when attempting to establish multiple
            // connections to the same server at the same time.  We never saw this in
            // the past because we were only using SshProxy to establish single connections
            // to any given server.
            //
            // This changed with thre [HiveFixture] implementation that attempts to
            // parallelize hive reset operations for better test execution performance.
            //
            // The symptom is that we see:
            //
            //      Renci.SshNet.Common.SshException:
            //          Message type 52 is not valid in the current context.
            //
            // sometimes while connecting or executing a command.
            //
            //-----------------------------------------------------------------
            // I found this issue from 2014: http://library759.rssing.com/chan-6841305/all_p61.html
            //
            // I run a bunch of threads (max 30) which all create a client and connect to the same server, and run a few commands.
            // All the clients use the same ConnectionInfo object to connect.
            //
            // The following exception occurs either on Connect, or RunCommand, but mostly on Connect.
            //      Renci.SshClient.Common.SshException: Message type 52 is not valid.
            //      at Renci.SshClient.Session.WaitHandle(WaitHandle waitHandle)
            //      at Renci.SshClient.PasswordConnectionInfo.OnAuthenticate()
            //      at Renci.SshClient.ConnectionInfo.Authenticate(Session session)
            //      at Renci.SshClient.Session.Connect()
            //      at Renci.SshClient.BaseClient.Connect()
            //      at upresources.LinuxSSH.Process(Object sender, DoWorkEventArgs e)
            //
            // I have seen this behaviour with the max number of threads set to 3, but that was in debug-mode in VS 2010.
            //
            // Please tell me if there's additional information I can provide.
            // Comments: ** Comment from web user: drieseng **
            //
            // I can reproduce this consistently when using the same ConnectionInfo for multiple connections (on multiple threads).
            //
            // This is because the authentication methods are not thread-safe. The EventWaitHandle and authentication result are on
            // the AuthenticationMethod, and - when its shared by multiple threads - it can lead to the exception mentioned in this issue.
            //
            // When the EventWaitHandle of an Authenticate invocation for a given session is actually getting set by the authentication 
            // attempt of another session, then SSH_MSG_USERAUTH_SUCCESS (and others) is unregistered while the session itself still has 
            // to receive this message. Once it receives the message, the exception above is thrown.
            //-----------------------------------------------------------------

            // $hack(jeff.lill):
            //
            // It appears that SSH.NET may assume that only a single connection attempt
            // to any given server will be in flight at any given time.  We're going to
            // mitigate this by allocating a lock object for each unique SSH service 
            // hostname and use that to ensure that only a single connection attempt
            // will be made at a time against any given server.
            //
            // This will result in a memory leak if you try to do something like crawl
            // the web with this :)

            lock (connectLocks)
            {
                if (connectLocks.TryGetValue(host, out var hostLock))
                {
                    return hostLock;
                }

                hostLock = new object();
                connectLocks.Add(host, hostLock);

                return hostLock;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        // SSH and SCP keep-alive ping interval.
        private const double KeepAliveSeconds = 15.0;

        // Used when logging redacted output.
        private const string Redacted = "!!SECRETS-REDACTED!!";

        // Path to the transient file on the Linux box whose presence indicates
        // that the server is still rebooting.
        private readonly string RebootStatusPath = $"{HiveHostFolders.Tmpfs}/rebooting";

        private object          syncLock   = new object();
        private bool            isDisposed = false;
        private SshCredentials  credentials;
        private SshClient       sshClient;
        private ScpClient       scpClient;
        private TextWriter      logWriter;
        private bool            isReady;
        private string          status;
        private bool            hasUploadFolder;
        private bool            hasDownloadFolder;
        private string          faultMessage;

        /// <summary>
        /// Constructs a <see cref="SshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="publicAddress">The public IP address or FQDN of the server or <c>null.</c></param>
        /// <param name="privateAddress">The private hive IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public SshProxy(string name, string publicAddress, IPAddress privateAddress, SshCredentials credentials, TextWriter logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(credentials != null);

            this.Name           = name;
            this.PublicAddress  = publicAddress;
            this.PrivateAddress = privateAddress;
            this.credentials    = credentials;
            this.logWriter      = logWriter;

            this.sshClient      = null;
            this.scpClient      = null;
            this.SshPort        = NetworkPorts.SSH;
            this.Status         = string.Empty;
            this.IsReady        = false;
            this.ConnectTimeout = TimeSpan.FromSeconds(5);
            this.FileTimeout    = TimeSpan.FromSeconds(30);
            this.RetryCount     = 10;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~SshProxy()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources (e.g. any open server connections).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources (e.g. any open server connections).
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (syncLock)
                {
                    if (!isDisposed)
                    {
                        Disconnect();

                        if (logWriter != null)
                        {
                            // $hack(jeff.lill):
                            //
                            // Sometimes we'll see an [ObjectDisposedException] here.  I'm
                            // not entirely sure why.  We'll mitigate this for now by catching
                            // and ignoring the exception.

                            try
                            {
                                logWriter.Dispose();
                            }
                            catch (ObjectDisposedException)
                            {
                                // Intentionally ignoring this.
                            }
                        }

                        isDisposed = true;
                    }
                }

                GC.SuppressFinalize(this);
            }

            isDisposed = true;
            logWriter  = null;
        }

        /// <summary>
        /// Returns a clone of the SSH proxy.  This can be useful for situations where you
        /// need to be able to perform multiple SSH/SCP operations against the same
        /// machine in parallel.
        /// </summary>
        /// <returns>The cloned <see cref="SshProxy{TMetadata}"/>.</returns>
        public SshProxy<TMetadata> Clone()
        {
            var sshProxy = new SshProxy<TMetadata>(Name, PublicAddress, PrivateAddress, credentials)
            {
                Metadata = this.Metadata
            };

            var connectionInfo = GetConnectionInfo();

            sshClient = new SshClient(connectionInfo);
            scpClient = new ScpClient(connectionInfo);

            return sshProxy;
        }

        /// <summary>
        /// Performs an action on a new thread, killing the thread if it hasn't
        /// terminated within the specified timeout.
        /// </summary>
        /// <param name="actionName">Idenfies the action for logging purposes.</param>
        /// <param name="action">The action to be performed.</param>
        /// <param name="timeout">The timeout.</param>
        private void DeadlockBreaker(string actionName, Action action, TimeSpan timeout)
        {
            // $todo(jeff.lill): 
            //
            // This is part of the mitigation for:
            //
            //      https://github.com/jefflill/NeonForge/issues/230
            //      https://github.com/sshnet/SSH.NET/issues/355

            var threadStart = new ThreadStart(action);
            var thread      = new Thread(threadStart);

            //LogLine($"*** DEADLOCK EXECUTE: {actionName}");

            thread.Start();

            if (!thread.Join(timeout))
            {
                //LogLine($"*** DEADLOCK BREAK: {actionName}");
                thread.Abort();
                //LogLine($"*** DEADLOCK BREAK COMPLETE: {actionName}");
            }
        }

        /// <summary>
        /// Closes any open connections to the Linux server but leaves open the
        /// opportunity to reconnect later.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This is similar to what dispose does <see cref="Dispose()"/> but dispose does
        /// not allow reconnection.
        /// </note>
        /// <para>
        /// This command is useful situations where the client application may temporarily
        /// lose contact with the server if for example, when it is rebooted or the network
        /// configuration changes.
        /// </para>
        /// </remarks>
        public void Disconnect()
        {
            // $todo(jeff.lill):
            //
            // We sometimes see a deadlock when disposing SSH.NET clients.
            //
            //      https://github.com/jefflill/NeonForge/issues/230
            //      https://github.com/sshnet/SSH.NET/issues/355
            //
            // I'm going to try to mitigate this by doing the dispose
            // on another thread and having that thread killed if it
            // it appears to be deadlocked.  Note that this will likely
            // result in resource leaks.

            var deadlockTimeout = TimeSpan.FromSeconds(30);

            lock (syncLock)
            {
                if (sshClient != null)
                {
                    try
                    {
                        if (sshClient.IsConnected)
                        {
                            DeadlockBreaker("SSH Client Dispose", () => sshClient.Dispose(), deadlockTimeout);
                        }
                    }
                    finally
                    {
                        sshClient = null;
                    }
                }

                if (scpClient != null)
                {
                    try
                    {
                        if (scpClient.IsConnected)
                        {
                            DeadlockBreaker("SCP Client Dispose", () => scpClient.Dispose(), deadlockTimeout);
                        }
                    }
                    finally
                    {
                        scpClient = null;
                    }
                }
            }
        }

        /// <summary>
        /// Disconnects the SSH client.
        /// </summary>
        private void InternalSshDisconnect()
        {
            if (sshClient != null)
            {
                try
                {
                    sshClient.Disconnect();
                    sshClient.Dispose();
                }
                finally
                {
                    sshClient = null;
                }
            }
        }

        /// <summary>
        /// Disconnects the SCP client.
        /// </summary>
        private void InternalScpDisconnect()
        {
            if (scpClient != null)
            {
                try
                {
                    scpClient.Disconnect();
                    scpClient.Dispose();
                }
                finally
                {
                    scpClient = null;
                }
            }
        }

        /// <summary>
        /// The associated <see cref="HiveProxy"/> or <c>null</c>.
        /// </summary>
        public HiveProxy Hive { get; internal set; }

        /// <summary>
        /// Returns the display name for the server.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the hive public IP address, FQDN, or <c>null</c> for the
        /// server.
        /// </summary>
        public string PublicAddress { get; private set; }

        /// <summary>
        /// Returns the hive private IP address to used for connecting to the server.
        /// </summary>
        public IPAddress PrivateAddress { get; set; }

        /// <summary>
        /// The SSH port.  This defaults to <b>22</b>.
        /// </summary>
        public int SshPort { get; set; }

        /// <summary>
        /// The connection attempt timeout.  This defaults to <b>5</b> seconds.
        /// </summary>
        public TimeSpan ConnectTimeout { get; set; }

        /// <summary>
        /// The file operation timeout.  This defaults to <b>30</b> seconds.
        /// </summary>
        public TimeSpan FileTimeout { get; set; }

        /// <summary>
        /// The number of times to retry a failed remote command.  
        /// This defaults to <b>5</b>.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Specifies the default options to be bitwise ORed with any specific
        /// options passed to a run or sudo execution command when the <see cref="RunOptions.Defaults"/> 
        /// flag is specified.  This defaults to <see cref="RunOptions.None"/>.
        /// </summary>
        /// <remarks>
        /// Setting this is a good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </remarks>
        public RunOptions DefaultRunOptions { get; set; } = RunOptions.None;

        /// <summary>
        /// The PATH to use on the remote server when executing commands in the
        /// session or <c>null</c>/empty to run commands without a path.  This
        /// defaults to the standard Linux path and <see cref="HiveHostFolders.Tools"/>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// When you modify this, be sure to use a colon (<b>:</b>) to separate 
        /// multiple directories as required.
        /// </note>
        /// </remarks>
        public string RemotePath { get; set; } = $"/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:{HiveHostFolders.Tools}";

        /// <summary>
        /// Returns the username used to log into the remote node.
        /// </summary>
        public string Username => credentials.Username;

        /// <summary>
        /// Updates the proxy credentials.  Call this whenever you change the
        /// password or SSH certificate for the user account we're using for the
        /// current proxy connection.  This ensures that the proxy will be able
        /// to reconnect to the service when required.
        /// </summary>
        /// <param name="newCredentials">The new credentials.</param>
        public void UpdateCredentials(SshCredentials newCredentials)
        {
            Covenant.Requires<ArgumentNullException>(newCredentials != null);

            this.credentials = newCredentials;
        }

        /// <summary>
        /// The current server status.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is intended to be used by management tools to indicate the state
        /// of the server for UX purposes.  This property will be set by some methods such
        /// as <see cref="WaitForBoot"/> but can also be set explicitly by tools when they
        /// have an operation in progress on the server.
        /// </para>
        /// <note>
        /// This will return <b>*** FAULTED ***</b> if the <see cref="IsFaulted"/>=<c>true</c>.
        /// </note>
        /// </remarks>
        public string Status
        {
            get
            {
                var result = status;

                if (IsFaulted)
                {
                    if (string.IsNullOrEmpty(faultMessage))
                    {
                        result = "*** FAULTED ***";
                    }
                    else
                    {
                        result = $"*** FAULT: {faultMessage}";
                    }
                }

                // Only return the first line of the status.

                result = result ?? string.Empty;

                var pos = result.IndexOfAny(new char[] { '\r', '\n' });

                if (pos != -1)
                {
                    result = result.Substring(0, pos).TrimEnd();
                }

                return result;
            }

            set
            {
                if (!string.IsNullOrEmpty(value) && value != status)
                {
                    if (IsFaulted)
                    {
                        LogLine($"*** STATUS[*FAULTED*]");
                    }
                    else
                    {
                        LogLine($"*** STATUS: {value}");
                    }
                }

                status = value;
            }
        }

        /// <summary>
        /// Indicates that the server has completed or has failed the current set of operations.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This will always return <c>false</c> if the server has faulted (<see cref="IsFaulted"/>=<c>true</c>).
        /// </note>
        /// </remarks>
        public bool IsReady
        {
            get { return IsFaulted || isReady; }
            set { isReady = value; }
        }

        /// <summary>
        /// Indicates that the server is in a faulted state because one or more operations
        /// have failed.
        /// </summary>
        public bool IsFaulted { get; private set; }

        /// <summary>
        /// Applications may use this to associate metadata with the instance.
        /// </summary>
        public TMetadata Metadata { get; set; }

        /// <summary>
        /// Shutdown the server.
        /// </summary>
        public void Shutdown()
        {
            Status = "shutting down...";

            try
            {
                SudoCommand("shutdown -h 0", RunOptions.Defaults | RunOptions.Shutdown);
                Disconnect();
            }
            catch (SshConnectionException)
            {
                // Sometimes we "An established connection was aborted by the server."
                // exceptions here, which we'll ignore (because we're shutting down).
            }
            finally
            {
                // Be very sure that the connections are cleared.

                sshClient = null;
                scpClient = null;
            }

            // Give the server a chance to stop.

            Thread.Sleep(TimeSpan.FromSeconds(10));
            Status = "stopped";
        }

        /// <summary>
        /// Reboot the server.
        /// </summary>
        /// <param name="wait">Optionally wait for the server to reboot (defaults to <c>true</c>).</param>
        public void Reboot(bool wait = true)
        {
            Status = "restarting...";

            // We need to be very sure that the remote server has actually 
            // rebooted and that we're not logging into the same session.
            // Originally, I just waited 10 seconds and assumed that the
            // SSH server (and maybe Linux) would have shutdown by then
            // so all I'd need to do is wait to reconnect.
            //
            // This was fragile and I have encountered situations where
            // SSH server was still running and the server hadn't restarted
            // after 10 seconds so I essentially reconnected to the server
            // with the reboot still pending.
            //
            // To ensure against this, I'm going to do the following:
            //
            //      1. Create a transient file at [/dev/shm/neon/rebooting]. 
            //         Since [/dev/shm] is a TMPFS, this file will no longer
            //         exist after a reboot.
            //
            //      2. Command the server to reboot.
            //
            //      3. Loop and attempt to reconnect.  After reconnecting,
            //         verify that the [/dev/shm/neon/rebooting] file is no
            //         longer present.  Reboot is complete if it's gone,
            //         otherwise, we need to continue trying.
            //
            //         We're also going to submit a new reboot command every 
            //         10 seconds when [/dev/shm/neon/rebooting] is still present
            //         in case the original reboot command was somehow missed
            //         because the reboot command is not retried automatically.
            //  
            //         Note that step #3 is actually taken care of in the
            //         [WaitForBoot()] method.

            try
            {
                SudoCommand($"mkdir -p {HiveHostFolders.Tmpfs} && touch {RebootStatusPath}");
                LogLine("*** REBOOT");
                SudoCommand("reboot", RunOptions.Defaults | RunOptions.Shutdown);
                LogLine("*** REBOOT submitted");
            }
            catch (SshConnectionException)
            {
                LogLine("*** REBOOT: SshConnectionException");
            }

            // Make sure we're disconnected.

            try
            {
                Disconnect();
            }
            catch (SshConnectionException)
            {
                // We'll ignore these because we're rebooting.
            }
            finally
            {
                // Be very sure that the connections are cleared.

                sshClient = null;
                scpClient = null;
            }

            // Give the server a chance to restart.

            Thread.Sleep(TimeSpan.FromSeconds(10));

            if (wait)
            {
                WaitForBoot();
            }
        }

        /// <summary>
        /// Writes text to the operation log.
        /// </summary>
        /// <param name="text">The text.</param>
        public void Log(string text)
        {
            if (logWriter != null)
            {
                logWriter.Write(text);
            }
        }

        /// <summary>
        /// Writes a line of text to the operation log.
        /// </summary>
        /// <param name="text">The text.</param>
        public void LogLine(string text)
        {
            if (logWriter != null)
            {
                logWriter.WriteLine(text);
                LogFlush();
            }
        }

        /// <summary>
        /// Flushes the log.
        /// </summary>
        public void LogFlush()
        {
            if (logWriter != null)
            {
                logWriter.Flush();
            }
        }

        /// <summary>
        /// Writes exception information to the operation log.
        /// </summary>
        /// <param name="e">The exception.</param>
        public void LogException(Exception e)
        {
            LogLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
            LogLine($"*** STACK: {e.StackTrace}");
        }

        /// <summary>
        /// Writes exception information to the operation log.
        /// </summary>
        /// <param name="message">The operation details.</param>
        /// <param name="e">The exception.</param>
        public void LogException(string message, Exception e)
        {
            LogLine($"*** ERROR: {message}: {NeonHelper.ExceptionError(e)}");
            LogLine($"*** STACK: {e.StackTrace}");
        }

        /// <summary>
        /// Puts the node proxy into the faulted state.
        /// </summary>
        /// <param name="message">The optional message to be logged.</param>
        public void Fault(string message = null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                faultMessage = message;
                LogLine("*** ERROR: " + message);
            }
            else
            {
                LogLine("*** ERROR: Unspecified FAULT");
            }

            IsFaulted = true;
        }

        /// <summary>
        /// Returns the connection information for SSH.NET.
        /// </summary>
        /// <returns>The connection information.</returns>
        private ConnectionInfo GetConnectionInfo()
        {
            var address   = string.Empty;
            var isPrivate = true;
            var port      = SshPort;

            if (Hive?.HostingManager != null)
            {
                var ep = Hive.HostingManager.GetSshEndpoint(this.Name);

                address = ep.Address;
                port    = ep.Port;
            }
            else if (Hive != null && Hive.UseNodePublicAddress)
            {
                address   = PublicAddress;
                isPrivate = false;
            }
            else
            {
                address = PrivateAddress.ToString();
            }

            if (string.IsNullOrEmpty(address))
            {
                var addressType = isPrivate ? "private" : "public";

                throw new Exception($"Node [{Name}] does not have a [{addressType}] address.");
            }

            var connectionInfo = new ConnectionInfo(address, port, credentials.Username, credentials.AuthenticationMethod)
            {
                Timeout = ConnectTimeout
            };

            // Ensure that we use a known good encryption mechanism.

            var encryptionName = "aes256-ctr";

            foreach (var disabledEncryption in connectionInfo.Encryptions
                .Where(e => e.Key != encryptionName)
                .ToList())
            {
                connectionInfo.Encryptions.Remove(disabledEncryption.Key);
            }

            return connectionInfo;
        }

        /// <summary>
        /// Establishes a connection to the server.
        /// </summary>
        /// <param name="timeout">Maximum amount of time to wait for a connection (defaults to <see cref="ConnectTimeout"/>).</param>
        public void Connect(TimeSpan timeout = default)
        {
            if (timeout == default(TimeSpan))
            {
                timeout = ConnectTimeout;
            }

            try
            {
                WaitForBoot(timeout);
            }
            catch (SshAuthenticationException e)
            {
                throw new HiveException("Access Denied: Invalid credentials.", e);
            }
            catch (Exception e)
            {
                throw new HiveException($"Unable to connect to the hive within [{timeout}].", e);
            }
        }

        /// <summary>
        /// Waits for the server to boot by continuously attempting to establish an SSH session.
        /// </summary>
        /// <param name="timeout">The operation timeout (defaults to <b>10 minutes</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <para>
        /// The method will attempt to connect to the server every 10 seconds up to the specified
        /// timeout.  If it is unable to connect during this time, the exception thrown by the
        /// SSH client will be rethrown.
        /// </para>
        /// </remarks>
        public void WaitForBoot(TimeSpan? timeout = null)
        {
            Covenant.Requires<ArgumentException>(timeout != null ? timeout >= TimeSpan.Zero : true);

            var operationTimer = new PolledTimer(timeout ?? TimeSpan.FromMinutes(10));

            while (true)
            {
                using (var sshClient = new SshClient(GetConnectionInfo()))
                {
                    try
                    {
                        LogLine($"*** WAITFORBOOT: CONNECT TO [{sshClient.ConnectionInfo.Host}:{sshClient.ConnectionInfo.Port}]");

                        lock (GetConnectLock(sshClient.ConnectionInfo.Host))
                        {
                            sshClient.Connect();
                        }

                        // We need to verify that the [/dev/shm/neon/rebooting] file is not present
                        // to ensure that the machine has actually restarted (see [Reboot()]
                        // for more information.

                        var response = sshClient.RunCommand($"if [ -f \"{RebootStatusPath}\" ] ; then exit 0; else exit 1; fi");

                        if (response.ExitStatus != 0)
                        {
                            // [/dev/shm/neon/rebooting] file is not present, so we're done.

                            break;
                        }
                        else
                        {
                            // It's possible that the original reboot command was lost
                            // and since that's not retried automatically, we'll resubmit
                            // it here.

                            try
                            {
                                LogLine("*** WAITFORBOOT: REBOOT");
                                sshClient.RunCommand("sudo reboot");
                            }
                            catch (Exception e)
                            {
                                // Intentionally ignoring any exceptions other than logging
                                // them because we're going to retry in the surrounding loop.

                                LogException("*** WAITFORBOOT[submit]", e);
                            }
                            finally
                            {
                                LogLine("*** WAITFORBOOT: REBOOT submitted");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogException("*** WAITFORBOOT[error]", e);

                        if (e is SshAuthenticationException)
                        {
                            // Don't retry if the credentials are bad.

                            throw;
                        }

                        if (operationTimer.HasFired)
                        {
                            throw;
                        }

                        LogLine($"*** WARNING: Wait for boot failed: {NeonHelper.ExceptionError(e)}");
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            Status = "online";
        }

        /// <summary>
        /// Opens a new <see cref="SshClient"/> connection.
        /// </summary>
        /// <returns>The new connection.</returns>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        private SshClient OpenSshConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SshProxy<TMetadata>));
                }

                if (credentials == null || credentials == SshCredentials.None)
                {
                    throw new HiveException("Cannot establish a SSH connection because no credentials are available.");
                }

                // We're going to retry connecting up to 10 times.

                const int maxTries = 10;

                var connectionInfo = GetConnectionInfo();

                for (int tryCount = 1; tryCount <= maxTries; tryCount++)
                {
                    var sshClient = new SshClient(connectionInfo)
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(KeepAliveSeconds)
                    };

                    try
                    {
                        lock (GetConnectLock(sshClient.ConnectionInfo.Host))
                        {
                            sshClient.Connect();
                        }

                        return sshClient;
                    }
                    catch (Exception e)
                    {
                        sshClient.Dispose();

                        if (e is SshAuthenticationException)
                        {
                            throw; // Fail immediately for bad credentials
                        }

                        if (tryCount == maxTries)
                        {
                            throw;
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                }

                throw new SshConnectionException($"Cannot connect SSH to: [host={connectionInfo.Host}, username={connectionInfo.Username}]");
            }
        }

        /// <summary>
        /// Ensures that an SSH connection has been established.
        /// </summary>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        private void EnsureSshConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SshProxy<TMetadata>));
                }

                if (credentials == null || credentials == SshCredentials.None)
                {
                    throw new HiveException("Cannot establish a SSH connection because no credentials are available.");
                }

                if (sshClient != null)
                {
                    if (sshClient.IsConnected)
                    {
                        return;
                    }
                    else
                    {
                        InternalSshDisconnect();
                    }
                }

                this.sshClient = OpenSshConnection();
            }
        }

        /// <summary>
        /// Opens a new <see cref="ScpClient"/> connection.
        /// </summary>
        /// <returns>The new connection.</returns>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        private ScpClient OpenScpConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SshProxy<TMetadata>));
                }

                // We're going to retry connecting up to 10 times.

                const int maxTries = 10;

                var connectionInfo = GetConnectionInfo();

                for (int tryCount = 1; tryCount <= maxTries; tryCount++)
                {
                    var scpClient = new ScpClient(connectionInfo)
                    {
                        OperationTimeout  = FileTimeout,
                        KeepAliveInterval = TimeSpan.FromSeconds(KeepAliveSeconds)
                    };

                    try
                    {
                        lock (GetConnectLock(scpClient.ConnectionInfo.Host))
                        {
                            scpClient.Connect();
                        }

                        return scpClient;
                    }
                    catch (Exception e)
                    {
                        scpClient.Dispose();

                        if (e is SshAuthenticationException)
                        {
                            throw; // Fail immediately for bad credentials
                        }

                        if (tryCount == maxTries)
                        {
                            throw;
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                }

                throw new SshConnectionException($"Cannot connect SCP to: [host={connectionInfo.Host}, username={connectionInfo.Username}]");
            }
        }

        /// <summary>
        /// Ensures that an SCP connection has been established.
        /// </summary>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        private void EnsureScpConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SshProxy<TMetadata>));
                }

                if (scpClient != null)
                {
                    if (scpClient.IsConnected)
                    {
                        return;
                    }
                    else
                    {
                        InternalScpDisconnect();
                    }
                }

                this.scpClient = OpenScpConnection();
            }
        }

        /// <summary>
        /// Returns the path to the user's home folder on the server.
        /// </summary>
        public string HomeFolderPath
        {
            get { return $"/home/{credentials.Username}"; }
        }

        /// <summary>
        /// Returns the path to the user's upload folder on the server.
        /// </summary>
        public string UploadFolderPath
        {
            get { return $"{HomeFolderPath}/.upload"; }
        }

        /// <summary>
        /// Ensures that the [~/.upload] folder exists on the server.
        /// </summary>
        private void EnsureUploadFolder()
        {
            if (!hasUploadFolder)
            {
                RunCommand($"mkdir -p {UploadFolderPath}", RunOptions.LogOnErrorOnly | RunOptions.IgnoreRemotePath);
                hasUploadFolder = true;
            }
        }

        /// <summary>
        /// Returns the path to the user's download folder on the server.
        /// </summary>
        public string DownloadFolderPath
        {
            get { return $"{HomeFolderPath}/.download"; }
        }

        /// <summary>
        /// Ensures that the [~/.download] folder exists on the server.
        /// </summary>
        private void EnsureDownloadFolder()
        {
            if (!hasDownloadFolder)
            {
                RunCommand($"mkdir -p {DownloadFolderPath}", RunOptions.LogOnErrorOnly | RunOptions.IgnoreRemotePath);
                hasDownloadFolder = true;
            }
        }

        /// <summary>
        /// <para>
        /// Creates and returns a clone of a low-level <see cref="SshClient"/> to 
        /// the remote endpoint.
        /// </para>
        /// <note>
        /// The caller is responsible for disposing the returned instance.
        /// </note>
        /// </summary>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        public SshClient CloneSshClient()
        {
            return OpenSshConnection();
        }

        /// <summary>
        /// <para>
        /// Creates and returns a clone of a low-level <see cref="ScpClient"/> to 
        /// the remote endpoint.
        /// </para>
        /// <note>
        /// The caller is responsible for disposing the returned instance.
        /// </note>
        /// </summary>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        public ScpClient CloneScpClient()
        {
            return OpenScpConnection();
        }

        /// <summary>
        /// Ensures that the configuration and setup folders required for a Neon host
        /// node exist and have the appropriate permissions.
        /// </summary>
        public void CreateHiveHostFolders()
        {
            Status = "prepare: host folders";

            // We need to be connected.

            EnsureSshConnection();
            EnsureScpConnection();

            // We need to create this folder first without using the safe SshProxy
            // SudoCommand/RunCommand methods because those methods depend on the 
            // existence of this folder.

            var result = sshClient.RunCommand($"sudo mkdir -p {HiveHostFolders.Exec}");

            if (result.ExitStatus != 0)
            {
                Log($"Cannot create folder [{HiveHostFolders.Exec}]\n");
                Log($"BEGIN-ERROR [{result.ExitStatus}]:\n");
                Log(result.Error);
                Log("END-ERROR:\n");
                throw new IOException(result.Error);
            }

            result = sshClient.RunCommand($"sudo chmod 777 {HiveHostFolders.Exec}");        // $todo(jeff.lill): Is this a potential security problem?
                                                                                            //                   SCP uploads fail for 770
            if (result.ExitStatus != 0)
            {
                Log($"Cannot chmod folder [{HiveHostFolders.Exec}]\n");
                Log($"BEGIN-ERROR [{result.ExitStatus}]:\n");
                Log(result.Error);
                Log("END-ERROR:\n");
                throw new IOException(result.Error);
            }

            // Create the folders.

            SudoCommand($"mkdir -p {HiveHostFolders.Archive}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Archive}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Bin}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Bin}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Config}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 755 {HiveHostFolders.Config}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Exec}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 777 {HiveHostFolders.Exec}", RunOptions.LogOnErrorOnly);    // $todo(jeff.lill): Is this a potential security problem?
                                                                                            //                   SCP uploads fail for 770
            SudoCommand($"mkdir -p {HiveHostFolders.Scripts}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Scripts}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Secrets}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Secrets}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Setup}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Setup}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Source}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Source}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.State}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.State}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.State}/setup", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.State}/setup", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {HiveHostFolders.Tools}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 750 {HiveHostFolders.Tools}", RunOptions.LogOnErrorOnly);

            // $hack(jeff.lill):
            //
            // All of a sudden I find that I need these folders too.

            SudoCommand("mkdir -p /home/root", RunOptions.LogOnErrorOnly);
            SudoCommand("chown root:root /home/root", RunOptions.LogOnErrorOnly);

            SudoCommand("mkdir -p /home/root/.archive", RunOptions.LogOnErrorOnly);
            SudoCommand("chmod 750 /home/root/.archive", RunOptions.LogOnErrorOnly);

            SudoCommand("mkdir -p /home/root/.download", RunOptions.LogOnErrorOnly);
            SudoCommand("chmod 777 /home/root/.download", RunOptions.LogOnErrorOnly);       // $todo(jeff.lill): Another potential security problem?

            SudoCommand("mkdir -p /home/root/.exec", RunOptions.LogOnErrorOnly);
            SudoCommand("chmod 777 /home/root/.exec", RunOptions.LogOnErrorOnly);

            SudoCommand("mkdir -p /home/root/.secrets", RunOptions.LogOnErrorOnly);
            SudoCommand("chmod 750 /home/root/.secrets", RunOptions.LogOnErrorOnly);

            SudoCommand("mkdir -p /home/root/.upload", RunOptions.LogOnErrorOnly);
            SudoCommand("chmod 777 /home/root/.upload", RunOptions.LogOnErrorOnly);         // $todo(jeff.lill): Another potential security problem?
        }

        /// <summary>
        /// Removes a file on the server if it exists.
        /// </summary>
        /// <param name="target">The path to the target file.</param>
        public void RemoveFile(string target)
        {
            var response = SudoCommand($"if [ -f \"{target}\" ] ; then rm \"{target}\" ; fi");

            if (response.ExitCode != 0)
            {
                throw new HiveException(response.ErrorSummary);
            }
        }

        /// <summary>
        /// Downloads a file from the Linux server and writes it out a stream.
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <param name="output">The output stream.</param>
        public void Download(string source, Stream output)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source));
            Covenant.Requires<ArgumentNullException>(output != null);

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Downloading: {source}");

            var downloadPath = $"{DownloadFolderPath}/{LinuxPath.GetFileName(source)}-{Guid.NewGuid().ToString("D")}";

            // We're not able to download some files directly due to permission issues 
            // so we'll make a temporary copy of the target file within the user's
            // home folder and then download that.  This is similar to what we had
            // to do for uploading.

            try
            {
                EnsureDownloadFolder();

                var response = SudoCommand("cp", source, downloadPath);

                if (response.ExitCode != 0)
                {
                    throw new HiveException(response.ErrorSummary);
                }

                response = SudoCommand("chmod", "444", downloadPath);

                if (response.ExitCode != 0)
                {
                    throw new HiveException(response.ErrorSummary);
                }

                SafeDownload(downloadPath, output);
            }
            catch (Exception e)
            {
                LogException("*** ERROR Downloading", e);
                throw;
            }
            finally
            {
                RemoveFile(downloadPath);
            }
        }

        /// <summary>
        /// Downloads a file as bytes from the Linux server .
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <returns>The file contents as UTF8 text.</returns>
        public byte[] DownloadBytes(string source)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source));

            using (var ms = new MemoryStream())
            {
                Download(source, ms);

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Downloads a file as text from the Linux server.
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <returns>The file contents as UTF8 text.</returns>
        public string DownloadText(string source)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source));

            using (var ms = new MemoryStream())
            {
                Download(source, ms);

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>
        /// Determines whether a directory exists on the remote server.
        /// </summary>
        /// <param name="path">The directory path.</param>
        /// <param name="runOptions">Optional command execution options.</param>
        /// <returns><c>true</c> if the directory exists.</returns>
        public bool DirectoryExists(string path, RunOptions runOptions = RunOptions.None)
        {
            var response = SudoCommand($"if [ -d \"{path}\" ] ; then exit 0; else exit 1; fi", runOptions);

            return response.ExitCode == 0;
        }

        /// <summary>
        /// Determines whether a file exists on the remote server.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        public bool FileExists(string path)
        {
            var response = SudoCommand($"if [ -f \"{path}\" ] ; then exit 0; else exit 1; fi", RunOptions.None);

            return response.ExitCode == 0;
        }

        /// <summary>
        /// Uploads a binary stream to the Linux server and then writes it to the file system.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="userPermissions">Optionally indicates that the operation should be performed with user-level permissions.</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        public void Upload(string target, Stream input, bool userPermissions = false)
        {
            Covenant.Requires<ArgumentNullException>(input != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target));

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Uploading: {target}");

            var uploadPath = $"{UploadFolderPath}/{LinuxPath.GetFileName(target)}-{Guid.NewGuid().ToString("D")}";

            try
            {
                EnsureUploadFolder();

                SafeUpload(input, uploadPath);

                SudoCommand($"mkdir -p {LinuxPath.GetDirectoryName(target)}", RunOptions.LogOnErrorOnly);

                if (userPermissions)
                {
                    RunCommand($"if [ -f {uploadPath} ]; then mv {uploadPath} {target}; fi", RunOptions.LogOnErrorOnly);
                }
                else
                {
                    SudoCommand($"if [ -f {uploadPath} ]; then mv {uploadPath} {target}; fi", RunOptions.LogOnErrorOnly);
                }
            }
            catch (Exception e)
            {
                LogException("*** ERROR Uploading", e);
                throw;
            }
            finally
            {
                // Ensure that the temporary file no longer exists (in case the move failed).

                RemoveFile(uploadPath);
            }
        }

        /// <summary>
        /// Uploads a byte array to a Linux server file.
        /// </summary>
        /// <param name="target">The target path of the file on the Linux server.</param>
        /// <param name="bytes">The bytes to be uploaded.</param>
        public void UploadBytes(string target, byte[] bytes)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target));

            if (bytes == null)
            {
                bytes = new byte[0];
            }

            using (var ms = new MemoryStream(bytes))
            {
                Upload(target, ms);
            }
        }

        /// <summary>
        /// Uploads a text stream to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="textStream">The input stream.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.
        /// </param>
        /// <param name="inputEncoding">Optionally specifies the input text encoding (defaults to UTF-8).</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// Any Unicode Byte Order Markers (BOM) at start of the input stream will be removed.
        /// </note>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        public void UploadText(string target, Stream textStream, int tabStop = 0, Encoding inputEncoding = null, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(textStream != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target));

            inputEncoding  = inputEncoding ?? Encoding.UTF8;
            outputEncoding = outputEncoding ?? Encoding.UTF8;

            using (var reader = new StreamReader(textStream, inputEncoding))
            {
                using (var binaryStream = new MemoryStream(64 * 1024))
                {
                    foreach (var line in reader.Lines())
                    {
                        var convertedLine = line;

                        if (tabStop != 0)
                        {
                            convertedLine = NeonHelper.ExpandTabs(convertedLine, tabStop: tabStop);
                        }

                        binaryStream.Write(outputEncoding.GetBytes(convertedLine));
                        binaryStream.WriteByte((byte)'\n');
                    }

                    binaryStream.Position = 0;
                    Upload(target, binaryStream);
                }
            }

            if (!string.IsNullOrEmpty(permissions))
            {
                SudoCommand("chmod", permissions, target);
            }
        }

        /// <summary>
        /// Uploads a text string to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">
        /// Optionally expands TABs into spaces when greater than zero or converts 
        /// a series of leading spaces into tabs if less than zero.
        /// </param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// <para>
        /// <b>Implementation Note:</b> The SSH.NET library we're using does not allow for
        /// files to be uploaded directly to arbitrary file system locations, even if the
        /// logged-in user has admin permissions.  The problem is that SSH.NET does not
        /// provide a way to use <b>sudo</b> to claim these higher permissions.
        /// </para>
        /// <para>
        /// The workaround is to create an upload folder in the user's home directory
        /// called <b>~/upload</b> and upload the file there first and then use SSH
        /// to move the file to its target location under sudo.
        /// </para>
        /// </note>
        /// </remarks>
        public void UploadText(string target, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target));

            using (var textStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                UploadText(target, textStream, tabStop, Encoding.UTF8, outputEncoding, permissions);
            }
        }

        /// <summary>
        /// Downloads a file from the remote node to the local file computer, creating
        /// parent folders as necessary.
        /// </summary>
        /// <param name="source">The source path on the Linux server.</param>
        /// <param name="target">The target path on the local computer.</param>
        public void Download(string source, string target)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target));

            if (IsFaulted)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target));

            LogLine($"*** Downloading: [{source}] --> [{target}]");

            try
            {
                using (var output = new FileStream(target, FileMode.Create, FileAccess.ReadWrite))
                {
                    SafeDownload(source, output);
                }
            }
            catch (Exception e)
            {
                LogException("*** ERROR Downloading", e);
                throw;
            }
        }

        /// <summary>
        /// Formats a Linux command and argument objects into a form suitable for passing
        /// to the <see cref="RunCommand(string, object[])"/> or <see cref="SudoCommand(string, object[])"/>
        /// methods.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The formatted command string.</returns>
        /// <remarks>
        /// This method quote arguments with embedded spaces and ignore <c>null</c> arguments.
        /// The method also converts arguments with types like <c>bool</c> into a Bash compatible
        /// form.
        /// </remarks>
        private string FormatCommand(string command, params object[] args)
        {
            var sb = new StringBuilder();

            sb.Append(command);

            if (args != null)
            {
                foreach (var arg in args)
                {
                    if (arg == null)
                    {
                        continue;
                    }

                    sb.Append(' ');

                    if (arg is bool)
                    {
                        sb.Append((bool)arg ? "true" : "false");
                    }
                    else if (arg is IEnumerable<string>)
                    {
                        // Expand string arrays into multiple arguments.

                        var first = true;

                        foreach (var value in (IEnumerable<string>)arg)
                        {
                            var valueString = value.ToString();

                            if (string.IsNullOrWhiteSpace(valueString))
                            {
                                valueString = "-"; // $todo(jeff.lill): Not sure if this makes sense any more.
                            }
                            else if (valueString.Contains(' '))
                            {
                                valueString = "\"" + valueString + "\"";
                            }

                            if (first)
                            {
                                first = false;
                            }
                            else
                            {
                                sb.Append(' ');
                            }

                            sb.Append(valueString);
                        }
                    }
                    else
                    {
                        var argString = arg.ToString();

                        if (string.IsNullOrWhiteSpace(argString))
                        {
                            argString = "-";
                        }
                        else if (argString.Contains(' '))
                        {
                            argString = "\"" + argString + "\"";
                        }

                        sb.Append(argString);
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Uploads a command bundle to the server and unpacks it to a temporary folder
        /// in the user's home folder.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The command execution options.</param>
        /// <param name="userPermissions">Indicates whether the upload should be performed with user or root permissions.</param>
        /// <returns>The path to the folder where the bundle was unpacked.</returns>
        private string UploadBundle(CommandBundle bundle, RunOptions runOptions, bool userPermissions)
        {
            Covenant.Requires<ArgumentNullException>(bundle != null);

            bundle.Validate();

            var executePermissions = userPermissions ? 777 : 700;

            using (var ms = new MemoryStream())
            {
                using (var zip = ZipFile.Create(ms))
                {
                    zip.BeginUpdate();

                    // Add the bundle files files to the ZIP archive we're going to upload.

                    foreach (var file in bundle)
                    {
                        var data = file.Data;

                        if (data == null && file.Text != null)
                        {
                            LogLine($"*** START TEXT FILE: {file.Path}");

                            if ((runOptions & RunOptions.Redact) != 0)
                            {
                                LogLine(Redacted);
                            }
                            else
                            {
                                using (var reader = new StringReader(file.Text))
                                {
                                    foreach (var line in reader.Lines())
                                    {
                                        LogLine(line);
                                    }
                                }
                            }

                            LogLine("*** END TEXT FILE");

                            data = Encoding.UTF8.GetBytes(file.Text);
                        }
                        else
                        {
                            LogLine($"*** BINARY FILE [length={data.Length}]: {file.Path}");
                        }

                        zip.Add(new StaticBytesDataSource(data), file.Path);
                    }

                    // Generate the "__run.sh" script file that will set execute permissions and
                    // then execute the bundle command.

                    var sb = new StringBuilder();

                    sb.AppendLineLinux("#!/bin/sh");
                    sb.AppendLineLinux();

                    foreach (var file in bundle.Where(f => f.IsExecutable))
                    {
                        if (file.Path.Contains(' '))
                        {
                            sb.AppendLineLinux($"chmod {executePermissions} \"{file.Path}\"");
                        }
                        else
                        {
                            sb.AppendLineLinux($"chmod {executePermissions} {file.Path}");
                        }
                    }

                    sb.AppendLineLinux(FormatCommand(bundle.Command, bundle.Args));

                    zip.Add(new StaticStringDataSource(sb.ToString()), "__run.sh");

                    LogLine($"*** START TEXT FILE: __run.sh");

                    if ((runOptions & RunOptions.Redact) != 0)
                    {
                        LogLine(Redacted);
                    }
                    else
                    {
                        using (var reader = new StringReader(sb.ToString()))
                        {
                            foreach (var line in reader.Lines())
                            {
                                LogLine(line);
                            }
                        }
                    }

                    LogLine("*** END TEXT FILE");

                    // Commit the changes to the ZIP stream.

                    zip.CommitUpdate();
                }

                // Upload the ZIP file to a temporary folder.

                var bundleFolder = $"{HiveHostFolders.Exec}/{Guid.NewGuid().ToString("D")}";
                var zipPath      = LinuxPath.Combine(bundleFolder, "__bundle.zip");

                SudoCommand($"mkdir -p", RunOptions.LogOnErrorOnly, bundleFolder);
                SudoCommand($"chmod 777", RunOptions.LogOnErrorOnly, bundleFolder);

                ms.Position = 0;
                Upload(zipPath, ms, userPermissions: true);

                // Unzip the bundle. 

                RunCommand($"unzip {zipPath} -d {bundleFolder}", RunOptions.LogOnErrorOnly);

                // Make [__run.sh] executable.

                SudoCommand($"chmod {executePermissions}", RunOptions.LogOnErrorOnly, LinuxPath.Combine(bundleFolder, "__run.sh"));

                return bundleFolder;
            }
        }

        /// <summary>
        /// Runs a shell command on the Linux server.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method uses <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse RunCommand(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            return RunCommand(command, DefaultRunOptions, args);
        }

        /// <summary>
        /// Attempts to perform a safe SCP operation up to
        /// [<see cref="RetryCount"/> + 1] times in the face of
        /// connection failures.
        /// </summary>
        /// <param name="name">The operation name (for logging).</param>
        /// <param name="action">The operation action.</param>
        private void SafeScpOperation(string name, Action action)
        {
            for (int i = 0; i <= RetryCount + 1; i++)
            {
                try
                {
                    EnsureScpConnection();
                    action();
                    return;
                }
                catch (SshConnectionException e)
                {
                    if (i < RetryCount)
                    {
                        LogLine($"WARNING: Safe SCP operation [{name}]: {NeonHelper.ExceptionError(e)}");
                        InternalScpDisconnect();
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Uploads a file while attempting to deal with transient connection issues.
        /// </summary>
        /// <param name="input">The source stream.</param>
        /// <param name="path">The target path.</param>
        private void SafeUpload(Stream input, string path)
        {
            SafeScpOperation("upload", () => scpClient.Upload(input, path));
        }

        /// <summary>
        /// Downloads a file while attempting to deal with transient connection issues.
        /// </summary>
        /// <param name="path">The source path.</param>
        /// <param name="output">The output stream.</param>
        private void SafeDownload(string path, Stream output)
        {
            SafeScpOperation("download", () => scpClient.Download(path, output));
        }

        /// <summary>
        /// Attempts to perform a safe SSH command operation up to
        /// [<see cref="RetryCount"/> + 1] times in the face of
        /// connection failures.
        /// </summary>
        /// <param name="name">The operation name (for logging).</param>
        /// <param name="action">The operation action.</param>
        private void SafeSshOperation(string name, Action action)
        {
            for (int i = 0; i <= RetryCount + 1; i++)
            {
                try
                {
                    EnsureSshConnection();
                    action();
                    return;
                }
                catch (SshConnectionException e)
                {
                    if (i < RetryCount)
                    {
                        LogLine($"WARNING: Safe SSH operation [{name}]: {NeonHelper.ExceptionError(e)}");
                        InternalSshDisconnect();
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Holds the result of a <see cref="SafeRunCommand(string, bool)"/> command execution.
        /// </summary>
        private class SafeSshCommand
        {
            public int      ExitStatus { get; set; }
            public string   Result { get; set; }
            public byte[]   ResultBinary { get; set; }
            public string   Error { get; set; }
        }

        /// <summary>
        /// Runs the command passed on the server to proactively deal with 
        /// transient connection issues.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="binaryOutput">Optionally indicates that the standard output should be treated as bunary.</param>
        /// <returns>The <see cref="SshCommand"/> response.</returns>
        /// <remarks>
        /// <note>
        /// The command may not specify file redirection (this is verified by
        /// <see cref="RunCommand(string, RunOptions, object[])"/>).
        /// </note>
        /// </remarks>
        private SafeSshCommand SafeRunCommand(string command, bool binaryOutput = false)
        {
            // Execute a simple echo command to ensure that the connection is
            // still established.  We're going to do this first to proactively 
            // handle the common case where idle connections are dropped by the
            // server or intervening firewalls and proxies.
            //
            // The code will close the connection when this is detected.

            if (sshClient != null)
            {
                if (!sshClient.IsConnected)
                {
                    LogLine("*** WARNING: Safe operation: Not connected");
                    InternalSshDisconnect();
                }
                else
                {
                    // Invoke an innocuous echo command to test the connection.

                    try
                    {
                        sshClient.RunCommand("echo ping");
                    }
                    catch (Exception e)
                    {
                        LogLine($"*** WARNING: Safe operation [PING]: {NeonHelper.ExceptionError(e)}");
                        InternalSshDisconnect();
                    }
                }
            }

            // Reestablish the connection (if necessary) and attempt to execute
            // the command up to [RetryCount+1] times.
            //
            // We're going to be tricky here and try to execute the command remotely
            // without assuming that the command is idempotent.  This means that we
            // need to track whether the command was invoked and also to verify that
            // it completed, potentially after we've been disconnected and then were
            // able to reestablish the connection.
            //
            // We're going to use the [/dev/shm] "shared memory" tmpfs to coordinate
            // this by:
            //
            //      1. Generating a GUID for the operation.
            //
            //      2. Creating a folder named [/dev/shm/neon/cmd/GUID] for the 
            //         operation.  This folder will be referred to as [$] below.
            //
            //      3. Generating a script called [$/cmd.sh] that 
            //         executes the COMMAND passed.  This script will:
            //
            //          a. Touch the [$/invoked] file just before 
            //             executing the command.
            //          b. Redirect STDOUT to [$/stdout].
            //          c. Redirect  STDERR to [$/stderr].
            //          d. Save the exit code to [$/exit]
            //             when the command completes.
            //
            //      4. If the connection is dropped while the command is executing,
            //         the code will reconnect and attempt to diagnose what happened
            //         and wait for the command to complete.
            //
            //      5. Retrieve the contents of the [$/exit], [$/stdout] and 
            //         [$/stderr] files.
            //
            //      6. Delete the [$] folder.
            //
            // In addition, the [neon-cleaner] service deployed to the host nodes will
            // periodically purge orphaned temporary command folders older than one day.

            // Create the command folder.

            var execFolder = $"{HiveHostFolders.Exec}/cmd";
            var cmdFolder  = LinuxPath.Combine(execFolder, Guid.NewGuid().ToString("D"));

            SafeSshOperation("create folder", () => sshClient.RunCommand($"mkdir -p {cmdFolder} && chmod 770 {cmdFolder}"));

            // Generate the command script.

            var script =
$@"# Safe command script.

touch {cmdFolder}/invoked
{command} 1> {cmdFolder}/stdout 2> {cmdFolder}/stderr
echo $? > {cmdFolder}/exit
";
            using (var msUpload = new MemoryStream(Encoding.UTF8.GetBytes(script.Replace("\r", string.Empty))))
            {
                SafeUpload(msUpload, LinuxPath.Combine(cmdFolder, "cmd.sh"));
            }

            // Execute the script.

            SafeSshOperation("execute script", () => sshClient.RunCommand($"if [ ! -f {LinuxPath.Combine(cmdFolder, "invoked")} ] ; then bash {LinuxPath.Combine(cmdFolder, "cmd.sh")}; fi;"));

            // Wait for the command to exit by looking for the [exit] file.
            // Note that if everything went the command will have completed
            // synchronously above and the [exit] file will be present for
            // the first iteration, so there shouldn't be too much delay.
            //
            // If we lost the connection while the command was executing,
            // we'll continue polling (with a brief delay) until the 
            // [exit] file appears.

            var finished = false;

            do
            {
                SafeSshOperation("waiting",
                    () =>
                    {
                        var response = sshClient.RunCommand($"if [ -f {LinuxPath.Combine(cmdFolder, "exit")} ] ; then exit 0; else exit 1; fi;");

                        if (response.ExitStatus == 0)
                        {
                            finished = true;
                            return;
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    });
            }
            while (!finished);

            // Retrieve the [exit], [stdout], and [stderr] files.

            var msExit   = new MemoryStream();
            var msStdOut = new MemoryStream();
            var msStdErr = new MemoryStream();

            SafeDownload(LinuxPath.Combine(cmdFolder, "exit"), msExit);
            SafeDownload(LinuxPath.Combine(cmdFolder, "stdout"), msStdOut);
            SafeDownload(LinuxPath.Combine(cmdFolder, "stderr"), msStdErr);

            msExit.Position   = 0;
            msStdOut.Position = 0;
            msStdErr.Position = 0;

            // Delete the temporary folder.

            SafeSshOperation("delete folder", () => sshClient.RunCommand($"rm -rf {cmdFolder}"));

            // Generate the result.

            return new SafeSshCommand()
            {
                ExitStatus   = int.Parse(Encoding.UTF8.GetString(msExit.ReadToEnd())),
                Result       = binaryOutput ? null : Encoding.UTF8.GetString(msStdOut.ReadToEnd()),
                ResultBinary = binaryOutput ? msStdOut.ReadToEnd() : null,
                Error        = Encoding.UTF8.GetString(msStdErr.ReadToEnd())
            };
        }

        /// <summary>
        /// Returns the command and arguments as a nicely formatted Bash command.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The command string.</returns>
        private string ToBash(string command, params object[] args)
        {
            return new CommandBundle(command, args).ToBash();
        }

        /// <summary>
        /// Runs a shell command on the Linux server with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <exception cref="RemoteCommandException">
        /// Thrown if the command returned a non-zero exit code and 
        /// <see cref="RunOptions.FaultOnError"/> was passed.
        /// </exception>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c> and throwing a <see cref="RemoteCommandException"/>.
        /// This means that <see cref="IsReady"/> will  always return <c>false</c> 
        /// afterwards and subsequent calls to <see cref="RunCommand(string, object[])"/>
        /// and <see cref="SudoCommand(string, object[])"/> will be ignored unless 
        /// <see cref="RunOptions.RunWhenFaulted"/> is passed with the future command. 
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// </remarks>
        public CommandResponse RunCommand(string command, RunOptions runOptions, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            if (command.Contains('<') || command.Contains('>'))
            {
                throw new ArgumentException($"[{nameof(SudoCommand)}(command,...)] does not support angle brackets (<) or (>).  Upload and run a [{nameof(CommandBundle)}] instead.");
            }

            var startLogged = false;

            command = FormatCommand(command, args);

            if (!string.IsNullOrWhiteSpace(RemotePath) && (runOptions & RunOptions.IgnoreRemotePath) == 0)
            {
                command = $"export PATH={RemotePath} && {command}";
            }

            if ((runOptions & RunOptions.Defaults) != 0)
            {
                runOptions |= DefaultRunOptions;
            }

            var runWhenFaulted = (runOptions & RunOptions.RunWhenFaulted) != 0;
            var logOnErrorOnly = (runOptions & RunOptions.LogOnErrorOnly) != 0 && (runOptions & RunOptions.LogOutput) == 0;
            var faultOnError   = (runOptions & RunOptions.FaultOnError) != 0;
            var binaryOutput   = (runOptions & RunOptions.BinaryOutput) != 0;
            var redact         = (runOptions & RunOptions.Redact) != 0;
            var logBundle      = (runOptions & RunOptions.LogBundle) != 0;
            var shutdown       = (runOptions & RunOptions.Shutdown) != 0;

            if (IsFaulted && !runWhenFaulted)
            {
                return new CommandResponse()
                {
                    Command        = command,
                    ExitCode       = 1,
                    ProxyIsFaulted = true,
                    ErrorText      = "** Hive node is faulted **"
                };
            }

            EnsureSshConnection();

            // Generate the command string we'll log by stripping out the 
            // remote PATH statement, if there is one.

            var commandToLog = command.Replace($"export PATH={RemotePath} && ", string.Empty);

            if (redact)
            {
                // Redact everything after the commmand word.

                var posEnd = commandToLog.IndexOf(' ');

                if (posEnd != -1)
                {
                    commandToLog = commandToLog.Substring(0, posEnd + 1) + Redacted;
                }
            }

            if (logBundle)
            {
                startLogged = true;
            }
            else if (!logOnErrorOnly)
            {
                LogLine($"START: {commandToLog}");

                startLogged = true;
            }

            SafeSshCommand  result;
            CommandResponse response;
            string          bashCommand = ToBash(command, args);

            if (shutdown)
            {
                // We just ran commands that shutdown or rebooted the server 
                // directly to prevent continuously rebooting the server.
                //
                // Because we're not using [SafeRunCommand()], there's some
                // risk that the connection is lost just before the command
                // is executed.  We're going to mitigate this by ensuring
                // that we have a fresh new SSH connection before invoking
                // the command.

                InternalSshDisconnect();
                EnsureSshConnection();

                var cmdResult = sshClient.RunCommand(command);

                response = new CommandResponse()
                {
                    Command     = command,
                    BashCommand = bashCommand,
                    ExitCode    = cmdResult.ExitStatus,
                    OutputText  = cmdResult.Result,
                    ErrorText   = cmdResult.Error
                };
            }
            else if (binaryOutput)
            {
                result   = SafeRunCommand($"{command}", binaryOutput: true);
                response = new CommandResponse()
                {
                    Command      = command,
                    BashCommand  = bashCommand,
                    ExitCode     = result.ExitStatus,
                    OutputBinary = result.ResultBinary,
                    ErrorText    = result.Error
                };
            }
            else
            {
                // Text output.

                result   = SafeRunCommand(command);
                response = new CommandResponse()
                {
                    Command     = command,
                    BashCommand = bashCommand,
                    ExitCode    = result.ExitStatus,
                    OutputText  = result.Result,
                    ErrorText   = result.Error
                };
            }

            var logEnabled = response.ExitCode != 0 || !logOnErrorOnly;

            if ((response.ExitCode != 0 && logOnErrorOnly) || (runOptions & RunOptions.LogOutput) != 0)
            {
                if (!startLogged)
                {
                    LogLine($"START: {commandToLog}");
                }

                if ((runOptions & RunOptions.LogOutput) != 0)
                {
                    if (binaryOutput)
                    {
                        var outputBinary = response.OutputBinary ?? new byte[0];

                        LogLine($"    BINARY OUTPUT [length={outputBinary.Length}]");
                    }
                    else
                    {
                        if (redact)
                        {
                            LogLine("    " + Redacted);
                        }
                        else
                        {
                            using (var reader = new StringReader(response.OutputText))
                            {
                                foreach (var line in reader.Lines())
                                {
                                    LogLine("    " + line);
                                }
                            }
                        }
                    }
                }
            }

            if (response.ExitCode != 0 || !logOnErrorOnly || (runOptions & RunOptions.LogOutput) != 0)
            {
                if (redact)
                {
                    LogLine("STDERR");
                    LogLine("    " + Redacted);
                }
                else
                {
                    using (var reader = new StringReader(response.ErrorText))
                    {
                        var extendedWritten = false;

                        foreach (var line in reader.Lines())
                        {
                            if (!extendedWritten)
                            {
                                LogLine("STDERR");
                                extendedWritten = true;
                            }

                            LogLine("    " + line);
                        }
                    }
                }

                if (response.ExitCode == 0)
                {
                    LogLine("END [OK]");
                }
                else
                {
                    LogLine($"END [ERROR={response.ExitCode}]");
                }

                if (response.ExitCode != 0)
                {
                    if (faultOnError)
                    {
                        Status    = $"ERROR[{response.ExitCode}]";
                        IsFaulted = true;

                        var message = redact ? "**REDACTED COMMAND**" : bashCommand;

                        throw new RemoteCommandException($"[exitcode={response.ExitCode}]: {message}");
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Runs a <see cref="CommandBundle"/> with user permissions on the remote machine.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The execution options (defaults to <see cref="RunOptions.Defaults"/>).</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="bundle"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method is intended for situations where one or more files need to be uploaded to a neonHIVE host node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// A good example of this is performing a <b>docker stack</b> command on the hive.  In this case, we need to
        /// upload the DAB file along with any files it references and then we we'll want to execute the Docker client.
        /// </para>
        /// <para>
        /// To use this class, construct an instance passing the command and arguments to be executed.  The command be 
        /// an absolute reference to an executable in folders such as <b>/bin</b> or <b>/usr/local/bin</b>, an executable
        /// somewhere on the current PATH, or relative to the files unpacked from the bundle.  The current working directory
        /// will be set to the folder where the bundle was unpacked, so you can reference local executables like
        /// <b>./MyExecutable</b>.
        /// </para>
        /// <para>
        /// Once a bundle is constructed, you will add <see cref="CommandFile"/> instances specifying the
        /// file data you want to include.  These include the relative path to the file to be uploaded as well
        /// as its text or binary data.  You may also indicate whether each file is to be marked as executable.
        /// </para>
        /// <note>
        /// <paramref name="runOptions"/> is set to <see cref="RunOptions.Defaults"/> by default.  This means
        /// that the flags specified by <see cref="DefaultRunOptions"/> will be be used.  This is a 
        /// good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </note>
        /// <note>
        /// This command requires that the <b>unzip</b> package be installed on the host.
        /// </note>
        /// </remarks>
        public CommandResponse RunCommand(CommandBundle bundle, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(bundle != null);

            // Write the START log line here so we can log the actual command being
            // executed and then disable this at the lower level, which would have 
            // logged the execution of the "__run.sh" script.

            LogLine("----------------------------------------");

            if ((runOptions & RunOptions.Redact) != 0)
            {
                LogLine($"START-BUNDLE: {Redacted}");
            }
            else
            {
                LogLine($"START-BUNDLE: {bundle}");
            }

            // Upload and extract the bundle and then run the "__run.sh" script.

            var bundleFolder = UploadBundle(bundle, runOptions, userPermissions: true);

            try
            {
                var response = RunCommand($"cd {bundleFolder} && ./__run.sh", runOptions | RunOptions.LogBundle);

                response.BashCommand = bundle.ToBash();

                LogLine($"END-BUNDLE");
                LogLine("----------------------------------------");

                return response;
            }
            finally
            {
                // Remove the bundle files.

                RunCommand($"rm -rf {bundleFolder}", RunOptions.RunWhenFaulted, RunOptions.LogOnErrorOnly);
            }
        }

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method uses the <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommand(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            return SudoCommand(command, DefaultRunOptions, args);
        }

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c>.  This means that <see cref="IsReady"/> will 
        /// always return <c>false</c> afterwards and subsequent command executions will be 
        /// ignored unless  <see cref="RunOptions.RunWhenFaulted"/> is specified for the 
        /// future command.
        /// </para>
        /// <para>
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommand(string command, RunOptions runOptions, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            command = FormatCommand(command, args);

            if (!string.IsNullOrWhiteSpace(RemotePath) && (runOptions & RunOptions.IgnoreRemotePath) == 0)
            {
                command = $"export PATH={RemotePath} && {command}";
            }

            var response = RunCommand($"sudo bash -c '{command}'", runOptions | RunOptions.IgnoreRemotePath);

            response.BashCommand = ToBash(command, args);

            return response;
        }

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> as a specific user.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="command">The command.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// This method uses the <see cref="DefaultRunOptions"/> when executing the command.
        /// </para>
        /// <para>
        /// You can override this behavior by passing an <see cref="RunOptions"/> to
        /// the <see cref="RunCommand(string, RunOptions, object[])"/> override.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommandAsUser(string user, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            return SudoCommandAsUser(user, command, DefaultRunOptions, args);
        }

        /// <summary>
        /// Runs a shell command on the Linux server under <b>sudo</b> as a specific user
        /// and with <see cref="RunOptions"/>.
        /// </summary>
        /// <param name="user">The username.</param>
        /// <param name="command">The command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The optional command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <note>
        /// <paramref name="command"/> may not include single quotes or redirect
        /// angle brackets such as <b>&lt;</b> or <b>>&gt;</b>.  For more complex
        /// command, try uploading and executing a <see cref="CommandBundle"/> instead.
        /// </note>
        /// <para>
        /// The <paramref name="runOptions"/> flags control how this command functions.
        /// If <see cref="RunOptions.FaultOnError"/> is set, then commands that return
        /// a non-zero exit code will put the server into the faulted state by setting
        /// <see cref="IsFaulted"/>=<c>true</c>.  This means that <see cref="IsReady"/> will 
        /// always return <c>false</c> afterwards and subsequent command executions will be 
        /// ignored unless  <see cref="RunOptions.RunWhenFaulted"/> is specified for the 
        /// future command.
        /// </para>
        /// <para>
        /// <see cref="RunOptions.LogOnErrorOnly"/> indicates that command output should
        /// be logged only for non-zero exit codes.
        /// </para>
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommandAsUser(string user, string command, RunOptions runOptions, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            command = $"sudo -u {user} bash -c '{command}'";

            var sbScript = new StringBuilder();

            sbScript.AppendLine("#!/bin/bash");

            if (!string.IsNullOrWhiteSpace(RemotePath) && (runOptions & RunOptions.IgnoreRemotePath) == 0)
            {
                sbScript.AppendLine($"export PATH={RemotePath}");
            }

            sbScript.AppendLine(command);

            var bundle = new CommandBundle("./script.sh");

            bundle.AddFile("script.sh", sbScript.ToString(), isExecutable: true);

            var response = SudoCommand(bundle, runOptions | RunOptions.IgnoreRemotePath);

            response.BashCommand = ToBash(command, args);

            return response;
        }

        /// <summary>
        /// Runs a <see cref="CommandBundle"/> under <b>sudo</b> on the remote machine.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The execution options (defaults to <see cref="RunOptions.Defaults"/>).</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method is intended for situations where one or more files need to be uploaded to a neonHIVE host node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// A good example of this is performing a <b>docker stack</b> command on the hive.  In this case, we need to
        /// upload the DAB file along with any files it references and then we we'll want to execute the Docker 
        /// client.
        /// </para>
        /// <para>
        /// To use this class, construct an instance passing the command and arguments to be executed.  The command be 
        /// an absolute reference to an executable in folders such as <b>/bin</b> or <b>/usr/local/bin</b>, an executable
        /// somewhere on the current PATH, or relative to the files unpacked from the bundle.  The current working directory
        /// will be set to the folder where the bundle was unpacked, so you can reference local executables like
        /// <b>./MyExecutable</b>.
        /// </para>
        /// <para>
        /// Once a bundle is constructed, you will add <see cref="CommandFile"/> instances specifying the
        /// file data you want to include.  These include the relative path to the file to be uploaded as well
        /// as its text or binary data.  You may also indicate whether each file is to be marked as executable.
        /// </para>
        /// <note>
        /// <paramref name="runOptions"/> is set to <see cref="RunOptions.Defaults"/> by default.  This means
        /// that the flags specified by <see cref="DefaultRunOptions"/> will be be used.  This is a 
        /// good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </note>
        /// <note>
        /// This command requires that the <b>unzip</b> package be installed on the host.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommand(CommandBundle bundle, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(bundle != null);

            // Write the START log line here so we can log the actual command being
            // executed and then disable this at the lower level, which would have 
            // logged the execution of the "__run.sh" script.

            LogLine("----------------------------------------");

            if ((runOptions & RunOptions.Redact) != 0)
            {
                LogLine($"START-BUNDLE: {Redacted}");
            }
            else
            {
                LogLine($"START-BUNDLE: {bundle}");
            }

            // Upload and extract the bundle and then run the "__run.sh" script.

            var bundleFolder = UploadBundle(bundle, runOptions, userPermissions: false);

            try
            {
                var response = SudoCommand($"cd {bundleFolder} && /bin/bash ./__run.sh", runOptions | RunOptions.LogBundle);

                response.BashCommand = bundle.ToBash();

                LogLine($"END-BUNDLE");
                LogLine("----------------------------------------");

                return response;
            }
            finally
            {
                // Remove the bundle files.

                SudoCommand($"rm -rf {bundleFolder}", runOptions);
            }
        }

        /// <summary>
        /// Runs a Docker command on the node under <b>sudo</b> with specific run options
        /// while attempting to handle transient errors.
        /// </summary>
        /// <param name="command">The Linux command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public CommandResponse DockerCommand(RunOptions runOptions, string command, params object[] args)
        {
            // $todo(jeff.lill): Hardcoding transient error handling for now.

            CommandResponse     response    = null;
            int                 attempt     = 0;
            int                 maxAttempts = 10;
            TimeSpan            delay       = TimeSpan.FromSeconds(15);
            string              orgStatus   = Status;

            while (attempt++ < maxAttempts)
            {
                response             = SudoCommand(command, runOptions, args);
                response.BashCommand = ToBash(command, args);

                if (response.ExitCode == 0)
                {
                    return response;
                }

                // Simple transitent error detection.

                if (response.ErrorText.Contains("i/o timeout") || response.ErrorText.Contains("Client.Timeout"))
                {
                    Status = $"[retry:{attempt}/{maxAttempts}]: {orgStatus}";
                    LogLine($"*** Waiting [{delay}] before retrying after a possible transient error.");

                    Thread.Sleep(delay);
                }
                else
                {
                    // Looks like a hard error.

                    if (runOptions.HasFlag(RunOptions.FaultOnError))
                    {
                        Fault(response.ErrorText.Trim());
                    }

                    return response;
                }
            }

            LogLine($"*** Operation failed after trying [{maxAttempts}] times.");

            if (runOptions.HasFlag(RunOptions.FaultOnError))
            {
                Fault();
            }

            return response;
        }

        /// <summary>
        /// Runs a Docker command on the node under <b>sudo</b> with <see cref="RunOptions.LogOutput"/>
        /// while attempting to handle transient errors.
        /// </summary>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public CommandResponse DockerCommand(string command, params object[] args)
        {
            return DockerCommand(RunOptions.LogOutput | RunOptions.Defaults, command, args);
        }

        /// <summary>
        /// Runs a Docker command as idempotent on the node under <b>sudo</b> with specific
        /// run options while attempting to handle transient errors.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="postAction">
        /// The action to be performed after the command was executed.  The
        /// <see cref="CommandResponse"/> from the command execution will be
        /// passed.  Pass <c>null</c> when there is no post-action.
        /// </param>
        /// <param name="command">The Linux command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public bool IdempotentDockerCommand(string actionId, Action<CommandResponse> postAction, RunOptions runOptions, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId));

            return InvokeIdempotentAction(actionId,
                () =>
                {
                    var response = DockerCommand(runOptions, command, args);

                    postAction?.Invoke(response);
                });
        }

        /// <summary>
        /// Runs a Docker command as idempotent on the node under <b>sudo</b>
        /// while attempting to handle transient errors.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="postAction">
        /// The action to be performed after the command was executed.  The
        /// <see cref="CommandResponse"/> from the command execution will be
        /// passed.  Pass <c>null</c> when there is no post-action.
        /// </param>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large hives.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public bool IdempotentDockerCommand(string actionId, Action<CommandResponse> postAction, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId));

            return InvokeIdempotentAction(actionId,
                () =>
                {
                    var response = DockerCommand(command, args);

                    postAction?.Invoke(response);
                });
        }

        /// <summary>
        /// Invokes a named action on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">Tbe action to be performed.</param>
        /// <returns><c>true</c> if the action was invoked.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="actionId"/> must uniquely identify the action on the node.
        /// This may include letters, digits, dashes and periods as well as one or
        /// more forward slashes that can be used to organize idempotent status files
        /// into folders.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="HiveHostFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public bool InvokeIdempotentAction(string actionId, Action action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId));
            Covenant.Requires<ArgumentNullException>(action != null);

            var stateFolder = HiveHostFolders.State;
            var slashPos    = actionId.LastIndexOf('/');

            if (slashPos != -1)
            {
                // Extract any folder path from the activity ID and add it to
                // the state folder path.

                stateFolder = LinuxPath.Combine(stateFolder, actionId.Substring(0, slashPos));
                actionId    = actionId.Substring(slashPos + 1);

                Covenant.Assert(actionId.Length > 0);
            }

            var statePath = LinuxPath.Combine(stateFolder, actionId);

            SudoCommand($"mkdir -p {stateFolder}");

            if (FileExists(statePath))
            {
                return false;
            }

            action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}");
            }

            return true;
        }

        /// <summary>
        /// Verifies a TLS/SSL certificate.
        /// </summary>
        /// <param name="name">The certificate name (included in errors).</param>
        /// <param name="certificate">The certificate being tested or <c>null</c>.</param>
        /// <param name="hostname">The hostname to be secured by the certificate.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// You may pass <paramref name="certificate"/> as <c>null</c> to indicate that no 
        /// checking is to be performed as a convienence.
        /// </remarks>
        public CommandResponse VerifyCertificate(string name, TlsCertificate certificate, string hostname)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            if (certificate == null)
            {
                return new CommandResponse() { ExitCode = 0 };
            }

            Status = $"verifying: [{name}] certificate";

            if (string.IsNullOrEmpty(hostname))
            {
                throw new ArgumentException($"No hostname is specified for the [{name}] certificate test.");
            }

            // Verify that the private key looks reasonable.

            if (!certificate.KeyPem.StartsWith("-----BEGIN PRIVATE KEY-----"))
            {
                throw new FormatException($"The [{name}] certificate's private key is not PEM encoded.");
            }

            // Verify the certificate.

            if (!certificate.CertPem.StartsWith("-----BEGIN CERTIFICATE-----"))
            {
                throw new ArgumentException($"The [{name}] certificate is not PEM encoded.");
            }

            // We're going to split the certificate into two files, the issued
            // certificate and the certificate authority's certificate chain
            // (AKA the CA bundle).
            //
            // Then we're going to upload these to [/tmp/cert.crt] and [/tmp/cert.ca]
            // and then use the [openssl] command to verify it.

            var pos = certificate.CertPem.IndexOf("-----END CERTIFICATE-----");

            if (pos == -1)
            {
                throw new ArgumentNullException($"The [{name}] certificate is not formatted properly.");
            }

            pos = certificate.CertPem.IndexOf("-----BEGIN CERTIFICATE-----", pos);

            var issuedCert = certificate.CertPem.Substring(0, pos);
            var caBundle   = certificate.CertPem.Substring(pos);

            try
            {
                UploadText("/tmp/cert.crt", issuedCert);
                UploadText("/tmp/cert.ca", caBundle);

                return SudoCommand(
                    "openssl verify",
                    RunOptions.FaultOnError,
                    "-verify_hostname", hostname,
                    "-purpose", "sslserver",
                    "-CAfile", "/tmp/cert.ca",
                    "/tmp/cert.crt");
            }
            finally
            {
                SudoCommand("rm -f /tmp/cert.*", RunOptions.LogOnErrorOnly);
            }
        }

        /// <summary>
        /// Creates an interactive shell.
        /// </summary>
        /// <returns>A <see cref="ShellStream"/>.</returns>
        public ShellStream CreateShell()
        {
            EnsureSshConnection();

            return sshClient.CreateShellStream("dumb", 80, 24, 800, 600, 1024);
        }

        /// <summary>
        /// Creates an interactive shell for running with <b>sudo</b> permissions. 
        /// </summary>
        /// <returns>A <see cref="ShellStream"/>.</returns>
        public ShellStream CreateSudoShell()
        {
            var shell = CreateShell();

            shell.WriteLine("sudo");

            return shell;
        }

        /// <summary>
        /// Returns the name of the network interface assigned to a specific IP address.
        /// </summary>
        /// <param name="address">The target IP address.</param>
        /// <returns>The network interface name.</returns>
        /// <exception cref="HiveException">Thrown if the interface was not found.</exception>
        /// <remarks>
        /// <para>
        /// In the olden days, network devices were assigned names like <b>eth0</b>,
        /// <b>eth1</b>,... during boot somewhat randomly and there was no guarantee
        /// that the same assignments would be made on subsequent server restarts.
        /// </para>
        /// <para>
        /// Modern Linux systems generate predictable network interfaces names during
        /// boot by enumerating the physical devices installed and generating device
        /// names based on the topology of the system (e.g. slots, channels,...).
        /// This is discussed <a href="https://www.freedesktop.org/wiki/Software/systemd/PredictableNetworkInterfaceNames/">here</a>.
        /// </para>
        /// <note>
        /// Cloud environments as well as environments where nodes hosted on hypervisors 
        /// like Hyper-V or XenServer will still assign interface names like <b>eth0</b>...
        /// This method will still work for these environments.
        /// </note>
        /// </remarks>
        public string GetNetworkInterface(IPAddress address)
        {
            Covenant.Requires<ArgumentNullException>(address != null);
            Covenant.Requires<ArgumentException>(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork, "Only IPv4 addresses are currently supported.");

            var result = SudoCommand("ip -o address");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot determine primary network interface via [ip -o address]: [exitcode={result.ExitCode}] {result.AllText}");
            }

            // $note(jeff.lill): We support only IPv4 addresses.

            // The [ip -o address] returns network interfaces on single lines that
            // will look something like:
            // 
            // 1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
            // 1: lo    inet6 ::1/128 scope host \       valid_lft forever preferred_lft forever
            // 2: enp2s0f0    inet 10.0.0.188/8 brd 10.255.255.255 scope global enp2s0f0\       valid_lft forever preferred_lft forever
            // 2: enp2s0f0    inet6 2601:600:a07f:fd61:1ec1:deff:fe6f:4a4/64 scope global mngtmpaddr dynamic \       valid_lft 308725sec preferred_lft 308725sec
            // 2: enp2s0f0    inet6 fe80::1ec1:deff:fe6f:4a4/64 scope link \       valid_lft forever preferred_lft forever
            //
            // We're going to look for the line with an [inet] (aka IPv4) address
            // that matches the node's private address.

            var regex = new Regex(@"^\d+:\s*(?<interface>[^\s]+)\s*inet\s*(?<address>[^/]+)", RegexOptions.IgnoreCase);

            using (var reader = new StringReader(result.OutputText))
            {
                foreach (var line in reader.Lines())
                {
                    var match = regex.Match(line);

                    if (match.Success && match.Groups["address"].Value == address.ToString())
                    {
                        return match.Groups["interface"].Value;
                    }
                }
            }

            throw new HiveException($"Cannot find network interface for [address={address}].");
        }

        /// <summary>
        /// Logs the node into a Docker registry.
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <returns><c>true</c> if the login succeeded.</returns>
        /// <remarks>
        /// <note>
        /// This does nothing but return <c>true</c> if the Docker public registry is 
        /// specified and the hive has registry caches deployed because the 
        /// caches handle authentication with the upstream registry in this case.
        /// </note>
        /// </remarks>
        public bool RegistryLogin(string registry, string username = null, string password = null)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.DnsHostRegex.IsMatch(registry));

            if (HiveHelper.IsDockerPublicRegistry(registry))
            {
                return true;
            }

            try
            {
                CommandBundle bundle;

                if (!string.IsNullOrEmpty(username))
                {
                    bundle = new CommandBundle("cat password.txt | docker login", "--username", username, "--password-stdin", registry);

                    bundle.AddFile("password.txt", password);
                }
                else if (HiveHelper.IsDockerPublicRegistry(registry))
                {
                    // Logging out of the Docker public registry is equivalent to 
                    // setting a NULL username.

                    bundle = new CommandBundle("docker logout", registry);
                }
                else
                {
                    // $todo(jeff.lill):
                    //
                    // I'm pretty sure that it's not possible to log into a 
                    // a non-Docker public registry without a username so we'll
                    // just return FALSE here.
                    //
                    // This could probably use some more research to be sure.
                    // For example, I believe that Elasticsearch has their own
                    // registry now and I don't know if they require an account.

                    return false;
                }

                var response = SudoCommand(bundle);

                return response.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Logs the node out of a Docker registry. 
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <returns><c>true</c> if the logout succeeded.</returns>
        /// <remarks>
        /// <note>
        /// This does nothing but return <c>true</c> if the Docker public registry is 
        /// specified and the hive has registry caches deployed because the 
        /// caches handle authentication with the upstream registry in this case.
        /// </note>
        /// </remarks>
        public bool RegistryLogout(string registry)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.DnsHostRegex.IsMatch(registry));

            if (HiveHelper.IsDockerPublicRegistry(registry))
            {
                return true;
            }

            try
            {
                var response = SudoCommand("docker logout", registry);

                return response.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Starts or restarts the <b>neon-registry-cache</b> container running on 
        /// the node with new upstream registry credentials.
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <returns><c>true</c> if the operation succeeded or was unnecessary.</returns>
        /// <remarks>
        /// <note>
        /// This method currently does nothing but return <c>true</c> for non-manager
        /// nodes or if the registry specified is not the Docker public registry
        /// because cache supports only the public registry or if the registry
        /// cache is not enabled for this hive.
        /// </note>
        /// </remarks>
        public bool RestartRegistryCache(string registry, string username = null, string password = null)
        {
            Covenant.Requires<ArgumentException>(HiveDefinition.DnsHostRegex.IsMatch(registry));

            username = username ?? string.Empty;
            password = password ?? string.Empty;

            // Return immediately if this is a NOP for the current node and environment.

            if (!HiveHelper.IsDockerPublicRegistry(registry) || !Hive.Definition.Docker.RegistryCache)
            {
                return true;
            }

            // $hack(jeff.lill)

            var nodeDefinition = Metadata as NodeDefinition;

            if (nodeDefinition == null || nodeDefinition.Role != NodeRole.Manager)
            {
                return true;
            }

            // Stop any existing registry cache container.  Note that we'll
            // also determine Docker image being used so we can restart with
            // the same one (if it has been changed since hive deployment).

            var image    = Hive.Definition.Image.RegistryCache;    // Default to this if there's no container.
            var response = DockerCommand(RunOptions.None, "docker", "ps", "-a", "--filter", "name=neon-registry-cache", "--format", "{{.Image}}");

            if (response.ExitCode != 0)
            {
                return false;
            }

            if (response.OutputText.Trim() != string.Empty)
            {
                image = response.OutputText.Trim();
            }

            response = DockerCommand(RunOptions.None, "docker", "rm", "--force", "neon-registry-cache");

            if (response.ExitCode != 0)
            {
                return false;
            }

            // Start/restart the registry cache.

            var runCommand = new CommandBundle(
                "docker run",
                "--name", "neon-registry-cache",
                "--detach",
                "--restart", "always",
                "--publish", $"{HiveHostPorts.DockerRegistryCache}:5000",
                "--volume", "/etc/neon-registry-cache:/etc/neon-registry-cache:ro",
                "--volume", "neon-registry-cache:/var/lib/neon-registry-cache",
                "--env", $"HOSTNAME={Name}.{Hive.Definition.Hostnames.RegistryCache}",
                "--env", $"REGISTRY={registry}",
                "--env", $"USERNAME={username}",
                "--env", $"PASSWORD={password}",
                "--env", "LOG_LEVEL=info",
                image);

            response = SudoCommand(runCommand);

            if (response.ExitCode != 0)
            {
                return false;
            }

            // Upload a script so it will be easier to manually restart the container.

            // $todo(jeff.lill);
            //
            // Persisting the registry credentials in the uploaded script here is 
            // probably not the best idea, but I really like the idea of having
            // this script available to make it easy to restart the cache if
            // necessary.
            //
            // There are a couple of mitigating factors:
            //
            //      * The scripts folder can only be accessed by the ROOT user
            //      * These are Docker public registry credentials only
            //
            // Users can create and use read-only credentials, which is 
            // probably a best practice anyway for most hives or they
            // can deploy a custom registry (whose crdentials will be 
            // persisted to Vault).

            UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-registry-cache.sh"), runCommand.ToBash(), permissions: "700");

            return true;
        }

        /// <summary>
        /// Waits for a specific hostname to resolve on the connected node.
        /// This is useful for ensuring that hive DNS host entry changes
        /// have been propagated by the [neon-dns-mon], [neon-dns], and
        /// PowerDNS services and are ready for use.
        /// </summary>
        /// <param name="hostname">The DNS hostname to be checked.</param>
        /// <param name="timeout">
        /// Optional timeout.  This defaults to 60 seconds.
        /// </param>
        /// <exception cref="TimeoutException">Thrown if the hostname didn't resolve in time.</exception>
        /// <remarks>
        /// <note>
        /// This method only verifies that the DNS hostnames resolves, not that
        /// it resolves to a specific value.  So you can't use this to ensure that
        /// a change to an existing DNS entry has been propagated.
        /// </note>
        /// </remarks>
        public void WaitForDnsHost(string hostname, TimeSpan timeout = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentNullException>(HiveDefinition.DnsHostRegex.IsMatch(hostname));

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(60);
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        var response = RunCommand("nslookup", RunOptions.None, hostname);

                        return response.ExitCode == 0;
                    },
                    timeout: timeout,
                    pollTime: TimeSpan.FromMilliseconds(500));
            }
            catch (TimeoutException)
            {
                // Re-throw with a nicer exception message.

                throw new TimeoutException($"Unable to resolve [{hostname}] within [{timeout}].");
            }
        }

        /// <summary>
        /// Returns the version of Docker installed on the node.
        /// </summary>
        /// <param name="faultIfNotInstalled">
        /// Optionally signal a node fault if the compontent is 
        /// not installed.
        /// </param>
        /// <returns>The Docker version or <c>null</c> if Docker is not installed.</returns>
        public SemanticVersion GetDockerVersion(bool faultIfNotInstalled = false)
        {
            // We're going execute this command:
            //
            //      docker version
            //
            // to obtain the version information.  This will return something like:
            //
            //      Client:
            //       Version:       18.03.0-ce
            //       API version:   1.37
            //       Go version:    go1.9.4
            //       Git commit:    0520e24
            //       Built: Wed Mar 21 23:10:01 2018
            //       OS/Arch:       linux/amd64
            //       Experimental:  false
            //       Orchestrator:  swarm
            //
            //      Server:
            //       Engine:
            //        Version:      18.03.0-ce
            //        API version:  1.37 (minimum version 1.12)
            //        Go version:   go1.9.4
            //        Git commit:   0520e24
            //        Built:        Wed Mar 21 23:08:31 2018
            //        OS/Arch:      linux/amd64
            //        Experimental: false
            //
            // We're going to extract the client version.

            var response = SudoCommand("docker version", RunOptions.None);

            if (response.ExitCode != 0)
            {
                return null;
            }

            var pattern = "Version:";
            var pos     = response.OutputText.IndexOf(pattern);

            if (pos == -1)
            {
                if (faultIfNotInstalled)
                {
                    Fault("DOCKER is not installed");
                }

                return null;
            }

            pos += pattern.Length;

            var posEnd = response.OutputText.IndexOf('\n', pos);

            if (posEnd == -1)
            {
                posEnd = response.OutputText.Length;
            }

            var version = response.OutputText.Substring(pos, posEnd - pos).Trim();

            return SemanticVersion.Parse(version);
        }

        /// <summary>
        /// Returns the version of HashiCorp Consul installed on the node.
        /// </summary>
        /// <returns>The Consul version or <c>null</c> if Consul is not installed.</returns>
        /// <param name="faultIfNotInstalled">
        /// Optionally signal a node fault if the compontent is 
        /// not installed.
        /// </param>
        public SemanticVersion GetConsulVersion(bool faultIfNotInstalled = false)
        {
            // We're going execute this command:
            //
            //      consul version
            //
            // to obtain the version information.  This will return something like:
            //
            //      Consul v1.1.0

            var response = SudoCommand("consul version", RunOptions.None);

            if (response.ExitCode != 0)
            {
                return null;
            }

            var pattern = "Consul v";
            var pos     = response.OutputText.IndexOf(pattern);

            if (pos == -1)
            {
                if (faultIfNotInstalled)
                {
                    Fault("CONSUL is not installed");
                }

                return null;
            }

            pos += pattern.Length;

            var posEnd = response.OutputText.IndexOf('\n', pos);

            if (posEnd == -1)
            {
                posEnd = response.OutputText.Length;
            }

            var version = response.OutputText.Substring(pos, posEnd - pos).Trim();

            return SemanticVersion.Parse(version);
        }

        /// <summary>
        /// Returns the version of HashiCorp Vault installed on the node.
        /// </summary>
        /// <param name="faultIfNotInstalled">
        /// Optionally signal a node fault if the compontent is 
        /// not installed.
        /// </param>
        /// <returns>The Vault version or <c>null</c> if Vault is not installed.</returns>
        public SemanticVersion GetVaultVersion(bool faultIfNotInstalled = false)
        {
            // We're going execute this command:
            //
            //      vault version
            //
            // to obtain the version information.  This will return something like:
            //
            //      Vault v0.10.1 ('756fdc4587350daf1c65b93647b2cc31a6f119cd')

            var response = SudoCommand("vault version", RunOptions.None);

            if (response.ExitCode != 0)
            {
                return null;
            }

            var pattern = "Vault v";
            var pos     = response.OutputText.IndexOf(pattern);

            if (pos == -1)
            {
                if (faultIfNotInstalled)
                {
                    Fault("VAULT is not installed");
                }

                return null;
            }

            pos += pattern.Length;

            var posEnd = response.OutputText.IndexOf(' ', pos);

            if (posEnd == -1)
            {
                posEnd = response.OutputText.Length;
            }

            var version = response.OutputText.Substring(pos, posEnd - pos).Trim();

            return SemanticVersion.Parse(version);
        }

        /// <summary>
        /// Returns the current time (UTC) on the remote machine.
        /// </summary>
        /// <returns>The machine's current <see cref="DateTime"/> (UTC).</returns>
        public DateTime GetTimeUtc()
        {
            var response = SudoCommand("date +%s", RunOptions.None);

            response.EnsureSuccess();

            var epochSeconds = long.Parse(response.OutputText.Trim());

            return NeonHelper.UnixEpoch + TimeSpan.FromSeconds(epochSeconds);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }
}
