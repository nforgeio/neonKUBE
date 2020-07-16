//-----------------------------------------------------------------------------
// FILE:	    SshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using Couchbase.Management;

// $todo(jefflill):
//
// The download methods don't seem to be working for paths like [/proc/meminfo].
// They return an empty stream.

namespace Neon.Kube
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
    /// Construct an instance to connect to a specific cluster node.  You may specify
    /// <typeparamref name="TMetadata"/> to associate application specific information
    /// or state with the instance.
    /// </para>
    /// <para>
    /// This class includes methods to invoke Linux commands on the node,
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
            // $hack(jefflill):
            //
            // SSH.NET appears to have an issue when attempting to establish multiple
            // connections to the same server at the same time.  We never saw this in
            // the past because we were only using SshProxy to establish single connections
            // to any given server.
            //
            // This changed with the [HiveFixture] implementation that attempts to
            // parallelize cluster reset operations for better test execution performance.
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

            // $hack(jefflill):
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
        private readonly string RebootStatusPath = $"{KubeHostFolders.Tmpfs}/rebooting";

        private object          syncLock   = new object();
        private bool            isDisposed = false;
        private SshCredentials  credentials;
        private SshClient       sshClient;
        private ScpClient       scpClient;
        private TextWriter      logWriter;
        private bool            isReady;
        private string          status;
        private string          faultMessage;

        /// <summary>
        /// Constructs a <see cref="SshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="publicAddress">The public IP address or FQDN of the server or <c>null.</c></param>
        /// <param name="privateAddress">The private cluster IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public SshProxy(string name, string publicAddress, IPAddress privateAddress, SshCredentials credentials, TextWriter logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(credentials != null, nameof(credentials));

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
                            // $hack(jefflill):
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
                Metadata  = this.Metadata,
                OsName    = this.OsName,
                OsVersion = this.OsVersion
            };

            var connectionInfo = GetConnectionInfo();

            sshClient = new SshClient(connectionInfo);
            scpClient = new ScpClient(connectionInfo);

            return sshProxy;
        }

        /// <summary>
        /// <para>
        /// Returns the name of the remote operating system (e.g. "Ubuntu").
        /// </para>
        /// <note>
        /// This is only valid after a connection has been established.
        /// </note>
        /// </summary>
        public string OsName { get; private set; }

        /// <summary>
        /// <para>
        /// Returns the version of the remote operating system (e.g. "18.04.1").
        /// </para>
        /// <note>
        /// This is only valid after a connection has been established.
        /// </note>
        /// </summary>
        public Version OsVersion { get; private set; }

        /// <summary>
        /// Performs an action on a new thread, killing the thread if it hasn't
        /// terminated within the specified timeout.
        /// </summary>
        /// <param name="actionName">Idenfies the action for logging purposes.</param>
        /// <param name="action">The action to be performed.</param>
        /// <param name="timeout">The timeout.</param>
        private void DeadlockBreaker(string actionName, Action action, TimeSpan timeout)
        {
            // $todo(jefflill): 
            //
            // This is part of the mitigation for:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/230
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
            // $todo(jefflill):
            //
            // We sometimes see a deadlock when disposing SSH.NET clients.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/230
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
        /// The associated <see cref="ClusterProxy"/> or <c>null</c>.
        /// </summary>
        public ClusterProxy Cluster { get; internal set; }

        /// <summary>
        /// Returns the display name for the server.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the cluster public IP address, FQDN, or <c>null</c> for the
        /// server.
        /// </summary>
        public string PublicAddress { get; private set; }

        /// <summary>
        /// Returns the cluster private IP address to used for connecting to the server.
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
        /// defaults to the standard Linux path and <see cref="KubeHostFolders.Bin"/>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// When you modify this, be sure to use a colon (<b>:</b>) to separate 
        /// multiple directories as required.
        /// </note>
        /// </remarks>
        public string RemotePath { get; set; } = $"/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/snap/bin:{KubeHostFolders.Bin}:{KubeHostFolders.Setup}";

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
            Covenant.Requires<ArgumentNullException>(newCredentials != null, nameof(newCredentials));

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
        /// <para>
        /// Prevents <b>sudo</b> from prompting for passwords.
        /// </para>
        /// <note>
        /// The connected user must already be a member of the <b>root</b> group.
        /// </note>
        /// <note>
        /// You do not need to call <see cref="Connect(TimeSpan)"/> or <see cref="WaitForBoot(TimeSpan?)"/>
        /// before calling this method (in fact, calling those methods will probably fail).
        /// </note>
        /// </summary>
        /// <param name="password">The current user's password.</param>
        public void DisableSudoPrompt(string password)
        {
            Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

            var connectionInfo = GetConnectionInfo();

            using (var sshClient = new SshClient(connectionInfo))
            {
                sshClient.Connect();

                using (var scpClient = new ScpClient(connectionInfo))
                {
                    scpClient.Connect();

                    var sudoDisableScript =
$@"#!/bin/bash

cat <<EOF > {KubeHostFolders.Home(Username)}/sudo-disable-prompt
#!/bin/bash
echo ""%sudo    ALL=NOPASSWD: ALL"" > /etc/sudoers.d/nopasswd

chown root /etc/sudoers.d/*
chmod 440 /etc/sudoers.d/*
EOF

chmod 770 {KubeHostFolders.Home(Username)}/sudo-disable-prompt

cat <<EOF > {KubeHostFolders.Home(Username)}/askpass
#!/bin/bash
echo {password}
EOF
chmod 770 {KubeHostFolders.Home(Username)}/askpass

export SUDO_ASKPASS={KubeHostFolders.Home(Username)}/askpass

sudo -A {KubeHostFolders.Home(Username)}/sudo-disable-prompt
rm {KubeHostFolders.Home(Username)}/sudo-disable-prompt
rm {KubeHostFolders.Home(Username)}/askpass
";
                    using (var stream = new MemoryStream())
                    {
                        stream.Write(Encoding.UTF8.GetBytes(sudoDisableScript.Replace("\r", string.Empty)));
                        stream.Position = 0;

                        scpClient.Upload(stream, $"{KubeHostFolders.Home(Username)}/sudo-disable");
                        sshClient.RunCommand($"chmod 770 {KubeHostFolders.Home(Username)}/sudo-disable");
                    }

                    sshClient.RunCommand($"{KubeHostFolders.Home(Username)}/sudo-disable");
                    sshClient.RunCommand($"rm {KubeHostFolders.Home(Username)}/sudo-disable");
                }
            }
        }

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
            // To ensure we avoid this, I'm going to do the following:
            //
            //      1. Create a transient file at [/dev/shm/neonkube/rebooting]. 
            //         Since [/dev/shm] is a TMPFS, this file will no longer
            //         exist after a reboot.
            //
            //      2. Command the server to reboot.
            //
            //      3. Loop and attempt to reconnect.  After reconnecting,
            //         verify that the [/dev/shm/neonkube/rebooting] file is no
            //         longer present.  Reboot is complete if it's gone,
            //         otherwise, we need to continue trying.
            //
            //         We're also going to submit a new reboot command every 
            //         10 seconds when [/dev/shm/neonkube/rebooting] is still present
            //         in case the original reboot command was somehow missed
            //         because the reboot command is not retried automatically.
            //  
            //         Note that step #3 is actually taken care of in the
            //         [WaitForBoot()] method.

            try
            {
                SudoCommand($"mkdir -p {KubeHostFolders.Tmpfs} && touch {RebootStatusPath}");
                LogLine("*** REBOOT");
                SudoCommand("systemctl stop systemd-logind.service", RunOptions.LogOutput);
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

            if (Cluster?.HostingManager != null)
            {
                var ep = Cluster.HostingManager.GetSshEndpoint(this.Name);

                address = ep.Address;
                port    = ep.Port;
            }
            else if (Cluster != null && !string.IsNullOrEmpty(PublicAddress))
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
        /// Establishes a connection to the server, disconnecting first if the proxy is already connected.
        /// </summary>
        /// <param name="timeout">Maximum amount of time to wait for a connection (defaults to <see cref="ConnectTimeout"/>).</param>
        /// <exception cref="SshProxyException">
        /// Thrown if the host hasn't been prepared yet and the SSH connection credentials are not username/password
        /// or if there's problem with low-level host configuration.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The first time a connection is established is called on a particular host, password credentials 
        /// must be used so that low-level <b>sudo</b> configuration cxan be performed.  Subsequent connections
        /// can use TLS certificates.
        /// </note>
        /// </remarks>
        public void Connect(TimeSpan timeout = default)
        {
            if (timeout == default(TimeSpan))
            {
                timeout = ConnectTimeout;
            }

            Disconnect();

            try
            {
                WaitForBoot(timeout);
            }
            catch (SshAuthenticationException e)
            {
                throw new SshProxyException("Access Denied: Invalid credentials.", e);
            }
            catch (Exception e)
            {
                throw new SshProxyException($"Unable to connect to the cluster within [{timeout}].", e);
            }
        }

        /// <summary>
        /// Waits for the server to boot by continuously attempting to establish an SSH session.
        /// </summary>
        /// <param name="timeout">The operation timeout (defaults to <b>10 minutes</b>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="SshProxyException">
        /// Thrown if the host hasn't been prepared yet and the SSH connection credentials are not username/password
        /// or if there's problem with low-level host configuration.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The first time a connection is established is called on a particular host, password credentials 
        /// must be used so that low-level <b>sudo</b> configuration cxan be performed.  Subsequent connections
        /// can use TLS certificates.
        /// </note>
        /// <para>
        /// The method will attempt to connect to the server every 10 seconds up to the specified
        /// timeout.  If it is unable to connect during this time, the exception thrown by the
        /// SSH client will be rethrown.
        /// </para>
        /// </remarks>
        public void WaitForBoot(TimeSpan? timeout = null)
        {
            Covenant.Requires<ArgumentException>(timeout != null ? timeout >= TimeSpan.Zero : true, nameof(timeout));

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

                        // Perform any required low-level host and user initialization.

                        PrepareHostAndUser();

                        // We need to verify that the [/dev/shm/neonkube/rebooting] file is not present
                        // to ensure that the machine has actually restarted (see [Reboot()]
                        // for more information.

                        var response = sshClient.RunCommand($"if [ -f \"{RebootStatusPath}\" ] ; then exit 0; else exit 1; fi");

                        if (response.ExitStatus != 0)
                        {
                            // [/dev/shm/neonkube/rebooting] file is not present, so we're done.

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

            Status = "connected";

            // Determine the remote operating name and version by examining the
            // [/etc/os-release] file.  This should look something like:
            //
            //    NAME="Ubuntu"
            //    VERSION="18.04.1 LTS (Bionic Beaver)"
            //    ID=ubuntu
            //    ID_LIKE=debian
            //    PRETTY_NAME="Ubuntu 18.04.1 LTS"
            //    VERSION_ID="18.04"
            //    HOME_URL="https://www.ubuntu.com/"
            //    SUPPORT_URL="https://help.ubuntu.com/"
            //    BUG_REPORT_URL="https://bugs.launchpad.net/ubuntu/"
            //    PRIVACY_POLICY_URL="https://www.ubuntu.com/legal/terms-and-policies/privacy-policy"
            //    VERSION_CODENAME=bionic
            //    UBUNTU_CODENAME=bionic

            try
            {
                var osRelease = DownloadText("/etc/os-release");

                using (var reader = new StringReader(osRelease))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var split = line.Split(new char[] { '=' }, 2);

                        if (split.Length < 2)
                        {
                            continue;
                        }

                        var name = split[0];
                        var value = split[1];

                        switch (name)
                        {
                            case "NAME":

                                OsName = value.Replace("\"", string.Empty);
                                break;

                            case "VERSION":

                                var version = value.Replace("\"", string.Empty);
                                var pSpace = version.IndexOf(' ');

                                if (pSpace != -1)
                                {
                                    version = version.Substring(0, pSpace);
                                }

                                OsVersion = new Version(version);
                                break;
                        }
                    }
                }
            }
            catch
            {
                // It is possible for this to fail when the host folders
                // haven't been created yet.

                OsName    = "unknown";
                OsVersion = new Version();
            }
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
                    throw new SshProxyException("Cannot establish a SSH connection because no credentials are available.");
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
                    throw new SshProxyException("Cannot establish a SSH connection because no credentials are available.");
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
        public string HomeFolderPath => KubeHostFolders.Home(Username);

        /// <summary>
        /// Returns the path to the user's download folder on the server.
        /// </summary>
        public string DownloadFolderPath => KubeHostFolders.Download(Username);

        /// <summary>
        /// Returns the path to the user's upload folder on the server.
        /// </summary>
        public string UploadFolderPath => KubeHostFolders.Upload(Username);

        /// <summary>
        /// <para>
        /// Creates and returns a clone of a low-level <see cref="SshClient"/> to 
        /// the remote endpoint.
        /// </para>
        /// <note>
        /// The caller is responsible for disposing the returned instance.
        /// </note>
        /// </summary>
        /// <returns>The cloned client.</returns>
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
        /// <returns>The cloned client.</returns>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        public ScpClient CloneScpClient()
        {
            return OpenScpConnection();
        }

        /// <summary>
        /// <para>
        /// Ensures that the node is configured such that <see cref="SshProxy{TMetadata}"/> can function properly.
        /// This includes disabling <b>requiretty</b> as well as restricting <b>sudo</b> from requiring passwords
        /// as well as creating the minimum user home folders required by the proxy for executing scripts as well
        /// as uploading and downloading files.
        /// </para>
        /// <para>
        /// This method creates the <b>/etc/sshproxy-init</b> file such that these operations will only
        /// be performed once.
        /// </para>
        /// </summary>
        /// <exception cref="SshProxyException">
        /// Thrown if the host hasn't been prepared yet and the SSH connection credentials are not username/password
        /// or if there's problem with low-level host configuration.
        /// </exception>
        /// <remarks>
        /// <note>
        /// The first time this method is called on a particular host, password credentials must be used so
        /// that low-level <b>sudo</b> configuration can be performed.  Subsequent connections can use
        /// TLS certificates.
        /// </note>
        /// </remarks>
        private void PrepareHostAndUser()
        {
            // We need to be connected.

            EnsureSshConnection();
            EnsureScpConnection();

            //-----------------------------------------------------------------
            // Ensure that the minimum set of user folders required by [SshProxy] exist
            // for the current user.  These are all located in the user's home folder
            // so SUDO is not required to create them.

            Status = "prepare: user folders";

            // [~/.neonkube]

            var folderPath = KubeHostFolders.NeonKubeHome(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neonkube/archive]

            folderPath = KubeHostFolders.Archive(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neonkube/download]

            folderPath = KubeHostFolders.Download(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neonkube/exec]

            folderPath = KubeHostFolders.Exec(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neonkube/upload]

            folderPath = KubeHostFolders.Upload(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            //-----------------------------------------------------------------
            // Disable SUDO password prompts if this hasn't already been done for this machine.
            // We can tell be checking whether this file exists:
            //
            //      /etc/sshproxy-init

            const string sshProxyInitPath = "/etc/sshproxy-init";

            if (!FileExists(sshProxyInitPath))
            {
                Status = "prepare: sudo";

                // We need to obtain the SSH password used to establish the current connection.  This means
                // that TLS based credentials won't work for the first connection to a host.  We're going
                // use reflection to get at the password itself.

                var authMethod = credentials.AuthenticationMethod as PasswordAuthenticationMethod;

                if (authMethod == null)
                {
                    throw new SshProxyException("You must use password credentials the first time you connect to a particular host machine.");
                }

                var passwordProperty = authMethod.GetType().GetProperty("Password", BindingFlags.Instance | BindingFlags.NonPublic);
                var passwordBytes    = (byte[])passwordProperty.GetValue(authMethod);
                var sshPassword      = Encoding.UTF8.GetString(passwordBytes);

                DisableSudoPrompt(sshPassword);

                // Indicate that we shouldn't perform these initialization operations again on this machine.

                sshClient.RunCommand($"sudo touch {sshProxyInitPath}");
            }
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
                throw new SshProxyException(response.ErrorSummary);
            }
        }

        /// <summary>
        /// Downloads a file from the Linux server and writes it out a stream.
        /// </summary>
        /// <param name="source">The source path of the file on the Linux server.</param>
        /// <param name="output">The output stream.</param>
        public void Download(string source, Stream output)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));
            Covenant.Requires<ArgumentNullException>(output != null, nameof(output));

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Downloading: {source}");

            var downloadPath = $"{DownloadFolderPath}/{LinuxPath.GetFileName(source)}-{Guid.NewGuid().ToString("d")}";

            // We're not able to download some files directly due to permission issues 
            // so we'll make a temporary copy of the target file within the user's
            // home folder and then download that.  This is similar to what we had
            // to do for uploading.

            try
            {
                var response = SudoCommand("cp", source, downloadPath);

                if (response.ExitCode != 0)
                {
                    throw new SshProxyException(response.ErrorSummary);
                }

                response = SudoCommand("chmod", "444", downloadPath);

                if (response.ExitCode != 0)
                {
                    throw new SshProxyException(response.ErrorSummary);
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));

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
        /// <returns><c>true</c> if the directory exists.</returns>
        public bool DirectoryExists(string path)
        {
            var response = SudoCommand($"if [ -d \"{path}\" ] ; then exit 0; else exit 1; fi");

            // $todo(jefflill):
            //
            // This doesn't really handle the case where the operation fails
            // due to a permissions restriction.

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

            // $todo(jefflill):
            //
            // This doesn't really handle the case where the operation fails
            // due to a permissions restriction.

            return response.ExitCode == 0;
        }

        /// <summary>
        /// Uploads a binary stream to the Linux server and then writes it to the file system.
        /// </summary>
        /// <param name="target">The target path on the Linux server.</param>
        /// <param name="input">The input stream.</param>
        /// <param name="permissions">Optionally specifies the file permissions (must be <c>chmod</c> compatible).</param>
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <param name="userPermissions">
        /// Optionally indicates that the operation should be performed with user-level permissions
        /// rather than <b>sudo</b>, which is the default.
        /// </param>
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
        public void Upload(string target, Stream input, string permissions = null, string owner = null, bool userPermissions = false)
        {
            Covenant.Requires<ArgumentNullException>(input != null, nameof(input));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Uploading: {target}");

            var uploadPath = $"{UploadFolderPath}/{LinuxPath.GetFileName(target)}-{Guid.NewGuid().ToString("d")}";

            try
            {
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

                if (!string.IsNullOrEmpty(permissions))
                {
                    SudoCommand("chmod", permissions, target);
                }

                if (!string.IsNullOrEmpty(owner))
                {
                    SudoCommand("chown", owner, target);
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            if (bytes == null)
            {
                bytes = Array.Empty<byte>();
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
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
        /// <remarks>
        /// <note>
        /// Any Unicode Byte Order Marker (BOM) at start of the input stream will be removed.
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
        public void UploadText(string target, Stream textStream, int tabStop = 0, Encoding inputEncoding = null, Encoding outputEncoding = null, string permissions = null, string owner = null)
        {
            Covenant.Requires<ArgumentNullException>(textStream != null, nameof(textStream));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

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
                    Upload(target, binaryStream, permissions: permissions, owner: owner);
                }
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
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
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
        public void UploadText(string target, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null, nameof(text));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            using (var textStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                UploadText(target, textStream, tabStop, Encoding.UTF8, outputEncoding, permissions: permissions, owner: owner);
            }
        }

        /// <summary>
        /// Uploads text from a <see cref="StringBuilder"/> to the Linux server and then writes it to the file system,
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
        /// <param name="owner">Optionally specifies the file owner (must be <c>chown</c> compatible).</param>
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
        public void UploadText(string target, StringBuilder text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null, nameof(text));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            UploadText(target, text.ToString(), tabStop: tabStop, outputEncoding: outputEncoding, permissions: permissions, owner: owner);
        }

        /// <summary>
        /// Downloads a file from the remote node to the local file computer, creating
        /// parent folders as necessary.
        /// </summary>
        /// <param name="source">The source path on the Linux server.</param>
        /// <param name="target">The target path on the local computer.</param>
        public void Download(string source, string target)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

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
                        sb.Append(NeonHelper.ToBoolString((bool)arg));
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
                                valueString = "-"; // $todo(jefflill): Not sure if this makes sense any more.
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
        /// <returns>The path to the folder where the bundle was unpacked.</returns>
        private string UploadBundle(CommandBundle bundle, RunOptions runOptions)
        {
            Covenant.Requires<ArgumentNullException>(bundle != null, nameof(bundle));

            bundle.Validate();

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
                        sb.AppendLineLinux($"chmod 700 \"{file.Path}\"");
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

                var bundleFolder = $"{KubeHostFolders.Exec(Username)}/{Guid.NewGuid().ToString("d")}";
                var zipPath      = LinuxPath.Combine(bundleFolder, "__bundle.zip");

                RunCommand($"mkdir {bundleFolder} && chmod 700 {bundleFolder}", RunOptions.LogOnErrorOnly);

                ms.Position = 0;
                Upload(zipPath, ms);

                // Unzip the bundle. 

                RunCommand($"unzip {zipPath} -d {bundleFolder}", RunOptions.LogOnErrorOnly);

                // Make [__run.sh] executable.

                RunCommand($"chmod 700", RunOptions.LogOnErrorOnly, LinuxPath.Combine(bundleFolder, "__run.sh"));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

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
            // We're going to use the [~/.neonkube/exec] folder coordinate
            // this by:
            //
            //      1. Generating a GUID for the operation.
            //
            //      2. Creating a folder named [~/.neonkube/exec] for the 
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

            var execFolder = $"{KubeHostFolders.Exec(Username)}";
            var cmdFolder  = LinuxPath.Combine(execFolder, Guid.NewGuid().ToString("d"));

            SafeSshOperation("create command folder", () => sshClient.RunCommand($"mkdir {cmdFolder} && chmod 700 {cmdFolder}"));

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
            // Note that if everything went well, the command will have completed
            // synchronously above and the [exit] file will be present for
            // the first iteration, so there shouldn't be too much delay.
            //
            // If we lost the connection while the command was executing,
            // we'll continue polling (with a brief delay) until the 
            // [exit] file appears.

            var finished = false;

            do
            {
                var abortedSshCommand = (SafeSshCommand)null;

                SafeSshOperation("waiting",
                    () =>
                    {
                        // We're also going to check to ensure that the [cmdFolder] still exists and
                        // fail the command and if it does not.  This will help mitigate situations 
                        // where the folder gets inadvertently deleted as happened with:
                        //
                        //      https://github.com/nforgeio/neonKUBE/issues/496
                        //
                        // We'll have the test command return 2 in this case to distinguish between
                        // the folder not being present from the exit file not being there.

                        var response = sshClient.RunCommand($"if [ ! -d {cmdFolder} ] ; then exit 2; fi; if [ -f {LinuxPath.Combine(cmdFolder, "exit")} ] ; then exit 0; else exit 1; fi;");

                        if (response.ExitStatus == 0)
                        {
                            finished = true;
                            return;
                        }
                        else if (response.ExitStatus == 2)
                        {
                            // The [cmdFolder] was deleted.

                            abortedSshCommand = new SafeSshCommand()
                            {
                                ExitStatus = 1,
                                Result     = string.Empty,
                                Error      = $"Command failed because the [{cmdFolder}] no longer exists."
                            };
                        }

                        Thread.Sleep(TimeSpan.FromSeconds(5));
                    });

                if (abortedSshCommand != null)
                {
                    return abortedSshCommand;
                }
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
        /// <returns>The Bash command string.</returns>
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

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
                    ErrorText      = "** Cluster node is faulted **"
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
                // directly to prevent the server from continuously rebooting.
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
                        var outputBinary = response.OutputBinary ?? Array.Empty<byte>();

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
        /// This method is intended for situations where one or more files need to be uploaded to a cluster host node 
        /// and then be used when a command is executed.
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
            Covenant.Requires<ArgumentNullException>(bundle != null, nameof(bundle));

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

            var bundleFolder = UploadBundle(bundle, runOptions);

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            command = $"sudo -u {user} bash -c '{command}'";

            var sbScript = new StringBuilder();

            sbScript.AppendLine("#!/bin/bash");

            if (!string.IsNullOrWhiteSpace(RemotePath) && (runOptions & RunOptions.IgnoreRemotePath) == 0)
            {
                sbScript.AppendLine($"export PATH={RemotePath}");
            }

            sbScript.AppendLine(command);

            var response = SudoCommand(CommandBundle.FromScript(sbScript), runOptions | RunOptions.IgnoreRemotePath);

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
        /// This method is intended for situations where one or more files need to be uploaded to a cluster host node 
        /// and then be used when a command is executed.
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
            Covenant.Requires<ArgumentNullException>(bundle != null, nameof(bundle));

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

            var bundleFolder = UploadBundle(bundle, runOptions);

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
        /// on the node at <see cref="KubeHostFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public bool InvokeIdempotentAction(string actionId, Action action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            var stateFolder = KubeHostFolders.State;
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (certificate == null)
            {
                return new CommandResponse() { ExitCode = 0 };
            }

            Status = $"verify: [{name}] certificate";

            if (string.IsNullOrEmpty(hostname))
            {
                throw new ArgumentException($"No hostname is specified for the [{name}] certificate test.", nameof(name));
            }

            // Verify that the private key looks reasonable.

            if (!certificate.KeyPem.StartsWith("-----BEGIN PRIVATE KEY-----"))
            {
                throw new FormatException($"The [{name}] certificate's private key is not PEM encoded.");
            }

            // Verify the certificate.

            if (!certificate.CertPem.StartsWith("-----BEGIN CERTIFICATE-----"))
            {
                throw new ArgumentException($"The [{name}] certificate is not PEM encoded.", nameof(name));
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
        /// <exception cref="SshProxyException">Thrown if the interface was not found.</exception>
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
            Covenant.Requires<ArgumentNullException>(address != null, nameof(address));
            Covenant.Requires<ArgumentException>(address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork, nameof(address), "Only IPv4 addresses are currently supported.");

            var result = SudoCommand("ip -o address");

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot determine primary network interface via [ip -o address]: [exitcode={result.ExitCode}] {result.AllText}");
            }

            // $note(jefflill): We support only IPv4 addresses.

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

            throw new SshProxyException($"Cannot find network interface for [address={address}].");
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

        /// <summary>
        /// Uses <c>kubectl apply -f</c> to apply a YAML file.
        /// </summary>
        /// <param name="yaml">The YAML file contents.</param>
        /// <param name="runOptions">Optional <see cref="RunOptions"/>.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        public CommandResponse KubectlApply(string yaml, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yaml), nameof(yaml));

            var bundle = new CommandBundle("kubectl apply -f file.yaml");

            bundle.AddFile("file.yaml", yaml);

            return SudoCommand(bundle, runOptions);
        }

        /// <summary>
        /// Uses <c>kubectl apply -f</c> to apply a YAML file.
        /// </summary>
        /// <param name="sbYaml">The YAML file contents.</param>
        /// <param name="runOptions">Optional <see cref="RunOptions"/>.</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        public CommandResponse KubeCtlApply(StringBuilder sbYaml, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(sbYaml != null, nameof(sbYaml));

            return KubectlApply(sbYaml.ToString());
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }
}
