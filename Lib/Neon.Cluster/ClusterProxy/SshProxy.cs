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

namespace Neon.Cluster
{
    /// <summary>
    /// Uses an SSH/SCP connection to provide access to Linux machines to access
    /// files, run commands, etc., typically for setup purposes.
    /// </summary>
    /// <typeparam name="TMetadata">
    /// Defines the metadata type the application wishes to associate with the server.
    /// You may specify <c>object</c> when no additional metadata is required.
    /// </typeparam>
    /// <threadsafety instance="false"/>
    /// <remarks>
    /// <para>
    /// Construct an instance to connect to a specific cluster node.  You may specify
    /// <typeparamref name="TMetadata"/> to associate application specific information
    /// or state with the instance.
    /// </para>
    /// <para>
    /// This class includes methods to invoke Linux commands on the node as well as
    /// methods to issue Docker commands against the local node or the Swarm cluster.
    /// Methods are also provided to upload and download files.
    /// </para>
    /// <para>
    /// Call <see cref="Dispose()"/> or <see cref="Disconnect()"/> to close the connection.
    /// </para>
    /// </remarks>
    public class SshProxy<TMetadata> : IDisposable
        where TMetadata : class
    {
        // SSH and SCP keep-alive ping interval.
        private const double KeepAliveSeconds = 15.0;

        // Used when logging redacted output.
        private const string Redacted = "!!SECRETS-REDACTED!!";

        // Path to the transiatent file on the Linux box whose presence indicates
        // that the server is still rebooting.
        private const string RebootStatusPath = "/dev/shm/neon/rebooting";

        private object          syncLock   = new object();
        private bool            isDisposed = false;
        private SshCredentials  credentials;
        private SshClient       sshClient;
        private ScpClient       scpClient;
        private TextWriter      logWriter;
        private bool            isReady;
        private string          status;
        private bool            hasUploadFolder;
        private bool            hasStateFolder;
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
            this.RetryCount     = 5;
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
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources (e.g. any open server connections).
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (!isDisposed)
                {
                    Disconnect();

                    if (logWriter != null)
                    {
                        logWriter.Dispose();
                        logWriter = null;
                    }

                    isDisposed = true;
                }
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
            lock (syncLock)
            {
                if (sshClient != null)
                {
                    try
                    {
                        if (sshClient.IsConnected)
                        {
                            sshClient.Dispose();
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
                            scpClient.Dispose();
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
        /// flag is specified.  This defaults to <see cref="RunOptions.LogOnErrorOnly"/>.
        /// </summary>
        /// <remarks>
        /// Setting this is a good way to specify a global default for flags like <see cref="RunOptions.FaultOnError"/>.
        /// </remarks>
        public RunOptions DefaultRunOptions { get; set; } = RunOptions.LogOnErrorOnly;

        /// <summary>
        /// The PATH to use on the remote server when executing commands in the
        /// session or <c>null</c>/empty to run commands without a path.  This
        /// defaults to the standard Linux path.
        /// </summary>
        /// <remarks>
        /// <note>
        /// When you modify this, be sure to use a colon (<b>:</b>) to separate 
        /// multiple directories as required.
        /// </note>
        /// </remarks>
        public string RemotePath { get; set; } = "/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/opt/neontools";

        /// <summary>
        /// Updates the proxy credentials.  Call this whenever you change the the
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
            get
            {
                return IsFaulted || isReady;
            }

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
            Status = "rebooting...";

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
            //      2. Actively ping the server with echo commands until I 
            //         catch a [SshConnectionException], indicating that the 
            //         server is probably rebotting,
            //
            //      3. Loop and attempt to reconnect.  After reconnecting,
            //         verify that the [/dev/shm/neon/rebooting] file is no
            //         longer present.  Reboot is complete if it's gone,
            //         otherwise, we need to continue trying.
            //  
            //         Note that step #3 is actually taken care of in the
            //         [WaitForBoot()] method.

            try
            {
                SudoCommand($"touch {RebootStatusPath}");
                SudoCommand("reboot", RunOptions.Defaults | RunOptions.Shutdown);
            }
            catch (SshConnectionException)
            {
                // Ignoring this.
            }

            // Wait for the SSH server to shutdown.

            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));

                try
                {
                    sshClient.RunCommand("bash echo ping");
                }
                catch (SshConnectionException)
                {
                    break;
                }
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
            else if (Cluster != null && Cluster.UseNodePublicAddress)
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
        public void Connect()
        {
            // Wait up to 60 seconds for the connection to be established.

            WaitForBoot(TimeSpan.FromSeconds(60));
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
                var sshClient = new SshClient(GetConnectionInfo());

                try
                {
                    sshClient.Connect();

                    // We need to verify that the [/dev/shm/neon/rebooting] file is not present
                    // to ensure that the machine has actually restarted (see [Reboot()]
                    // for more information.

                    var response = sshClient.RunCommand($"if [ -f \"{RebootStatusPath}\" ] ; then exit 0; else exit 1; fi");

                    if (response.ExitStatus != 0)
                    {
                        // [/dev/shm/neon/rebooting] file is not present, so we're done.

                        sshClient.Dispose();
                        break;
                    }
                }
                catch (Exception e)
                {
                    sshClient.Dispose();

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

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            Status = "online";
        }

        /// <summary>
        /// Ensures that an SSH connection has been established.
        /// </summary>
        private void EnsureSshConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(SshProxy<TMetadata>));
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

                // We're going to retry connecting up to 10 times.

                const int maxTries = 10;

                for (int tryCount = 1; tryCount <= maxTries; tryCount++)
                {
                    var sshClient = new SshClient(GetConnectionInfo())
                    {
                        KeepAliveInterval = TimeSpan.FromSeconds(KeepAliveSeconds)
                    };

                    try
                    {
                        sshClient.Connect();

                        this.sshClient = sshClient;
                        return;
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
            }
        }

        /// <summary>
        /// Ensures that an SCP connection has been established.
        /// </summary>
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

                // We're going to retry connecting up to 10 times.

                const int maxTries = 10;

                for (int tryCount = 1; tryCount <= maxTries; tryCount++)
                {
                    var scpClient = new ScpClient(GetConnectionInfo())
                    {
                        OperationTimeout  = FileTimeout,
                        KeepAliveInterval = TimeSpan.FromSeconds(KeepAliveSeconds)
                    };

                    try
                    {
                        scpClient.Connect();

                        this.scpClient = scpClient;
                        return;
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
        /// Ensures that the [~/upload] folder exists on the server.
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
        /// Ensures that the configuration and setup folders required for a Neon host
        /// node exist and have the appropriate permissions.
        /// </summary>
        public void InitializeNeonFolders()
        {
            Status = "prepare: folders";

            SudoCommand($"mkdir -p {NodeHostFolders.Config}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Config}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Secrets}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Secrets}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.State}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.State}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Setup}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Setup}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Tools}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Tools}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Scripts}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Scripts}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Archive}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 600 {NodeHostFolders.Archive}", RunOptions.LogOnErrorOnly);

            SudoCommand($"mkdir -p {NodeHostFolders.Exec}", RunOptions.LogOnErrorOnly);
            SudoCommand($"chmod 777 {NodeHostFolders.Exec}", RunOptions.LogOnErrorOnly);   // Allow non-[sudo] access.
        }

        /// <summary>
        /// Downloads a file from the Linux server and writes it out a stream.
        /// </summary>
        /// <param name="path">The source path of the file on the Linux server.</param>
        /// <param name="output">The output stream.</param>
        public void Download(string path, Stream output)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));
            Covenant.Requires<ArgumentNullException>(output != null);

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Downloading: {path}");

            try
            {
                SafeDownload(path, output);
            }
            catch (Exception e)
            {
                LogException("*** ERROR Downloading", e);
                throw;
            }
        }

        /// <summary>
        /// Downloads a file as text from the Linux server .
        /// </summary>
        /// <param name="path">The source path of the file on the Linux server.</param>
        /// <returns>The file contents as UTF8 text.</returns>
        public string DownloadText(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            using (var ms = new MemoryStream())
            {
                Download(path, ms);

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
        /// <param name="runOptions">Optional command execution options.</param>
        /// <returns><c>true</c> if the file exists.</returns>
        public bool FileExists(string path, RunOptions runOptions = RunOptions.None)
        {
            var response = SudoCommand($"if [ -f \"{path}\" ] ; then exit 0; else exit 1; fi", runOptions);

            return response.ExitCode == 0;
        }

        /// <summary>
        /// Uploads a binary stream to the Linux server and then writes it to the file system.
        /// </summary>
        /// <param name="path">The target path on the Linux server.</param>
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
        public void Upload(string path, Stream input, bool userPermissions = false)
        {
            Covenant.Requires<ArgumentNullException>(input != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            if (IsFaulted)
            {
                return;
            }

            LogLine($"*** Uploading: {path}");

            try
            {
                EnsureUploadFolder();

                var uploadPath = $"{UploadFolderPath}/{LinuxPath.GetFileName(path)}";

                SafeUpload(input, uploadPath);

                SudoCommand($"mkdir -p {LinuxPath.GetDirectoryName(path)}", RunOptions.LogOnErrorOnly);

                if (userPermissions)
                {
                    RunCommand($"if [ -f {uploadPath} ]; then mv {uploadPath} {path}; fi", RunOptions.LogOnErrorOnly);
                }
                else
                {
                    SudoCommand($"if [ -f {uploadPath} ]; then mv {uploadPath} {path}; fi", RunOptions.LogOnErrorOnly);
                }
            }
            catch (Exception e)
            {
                LogException("*** ERROR Uploading", e);
                throw;
            }
        }

        /// <summary>
        /// Uploads a text stream to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="path">The target path on the Linux server.</param>
        /// <param name="textStream">The input stream.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="inputEncoding">Optionally specifies the input text encoding (defaults to UTF-8).</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
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
        public void UploadText(string path, Stream textStream, int tabStop = 0, Encoding inputEncoding = null, Encoding outputEncoding = null)
        {
            Covenant.Requires<ArgumentNullException>(textStream != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            inputEncoding  = inputEncoding ?? Encoding.UTF8;
            outputEncoding = outputEncoding ?? Encoding.UTF8;

            using (var reader = new StreamReader(textStream, inputEncoding))
            {
                using (var binaryStream = new MemoryStream(64 * 1024))
                {
                    foreach (var line in reader.Lines())
                    {
                        var convertedLine = line;

                        if (tabStop > 0)
                        {
                            convertedLine = NeonHelper.ExpandTabs(convertedLine, tabStop: tabStop);
                        }

                        binaryStream.Write(outputEncoding.GetBytes(convertedLine));
                        binaryStream.WriteByte((byte)'\n');
                    }

                    binaryStream.Position = 0;
                    Upload(path, binaryStream);
                }
            }
        }

        /// <summary>
        /// Uploads a text string to the Linux server and then writes it to the file system,
        /// converting any CR-LF line endings to the Unix-style LF.
        /// </summary>
        /// <param name="path">The target path on the Linux server.</param>
        /// <param name="text">The input text.</param>
        /// <param name="tabStop">Optionally expands TABs into spaces when non-zero.</param>
        /// <param name="outputEncoding">Optionally specifies the output text encoding (defaults to UTF-8).</param>
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
        public void UploadText(string path, string text, int tabStop = 0, Encoding outputEncoding = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path));

            using (var textStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                UploadText(path, textStream, tabStop, Encoding.UTF8, outputEncoding);
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
        /// Uploads a Mono compatible executable to the server and generates a Bash script 
        /// that seamlessly executes it.
        /// </summary>
        /// <param name="sourcePath">The path to the source executable on the local machine.</param>
        /// <param name="targetName">The name for the target command on the server (without a folder path or file extension).</param>
        /// <param name="targetFolder">The optional target folder on the server (defaults to <b>/usr/local/bin</b>).</param>
        /// <param name="permissions">
        /// The Linux file permissions.  This defaults to <b>"700"</b> which grants only the current user
        /// read/write/execute permissions.
        /// </param>
        /// <remarks>
        /// <para>
        /// This method does the following:
        /// </para>
        /// <list type="number">
        /// <item>Uploads the executable to the target folder and names it <paramref name="targetName"/><b>.mono</b>.</item>
        /// <item>Creates a bash script in the target folder called <paramref name="targetName"/> that executes the Mono file.</item>
        /// <item>Makes the script executable.</item>
        /// </list>
        /// </remarks>
        public void UploadMonoExecutable(string sourcePath, string targetName, string targetFolder = "/usr/local/bin", string permissions = "700")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sourcePath));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetFolder));

            using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                var binaryPath = LinuxPath.Combine(targetFolder, $"{targetName}.mono");

                Upload(binaryPath, input);

                // Set the permissions on the binary that match those we'll set
                // for the wrapping script, except stripping off the executable
                // flags.

                var binaryPermissions = new LinuxPermissions(permissions);

                binaryPermissions.OwnerExecute = false;
                binaryPermissions.GroupExecute = false;
                binaryPermissions.AllExecute   = false;

                SudoCommand($"chmod {binaryPermissions} {binaryPath}", RunOptions.LogOnErrorOnly);
            }

            var scriptPath = LinuxPath.Combine(targetFolder, targetName);
            var script =
$@"#!/bin/bash
#------------------------------------------------------------------------------
# Seamlessly invokes the [{targetName}.mono] executable using the Mono
# runtime, passing any arguments along.

mono {scriptPath}.mono $@
";

            UploadText(scriptPath, script, tabStop: 4);
            SudoCommand($"chmod {permissions} {scriptPath}", RunOptions.LogOnErrorOnly);
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

                var bundleFolder = $"{NodeHostFolders.Exec}/{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss.fff")}";
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
            // In addition, the code deletes any directories like [/dev/shm/neon/cmd/*]
            // that are older than one day to help ensure that we don't fill up the
            // [/dev/shm] file system.

            // This bit of magic removes folders older than a day.

            var shmFolder = "/dev/shm/neon/cmd";

            SafeSshOperation("purge old folders", () => sshClient.RunCommand($@"find {shmFolder}. ! -name . -type d -mtime +0 -exec rm -rf {{}} \; -prune"));

            // Create the command folder.

            var cmdFolder = LinuxPath.Combine(shmFolder, Guid.NewGuid().ToString("D"));

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
        /// <see cref="IsFaulted"/>=<c>true</c>.  This means that <see cref="IsReady"/> will 
        /// always return <c>false</c> afterwards and subsequent calls to <see cref="RunCommand(string, object[])"/>
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

            SafeSshCommand      result;
            CommandResponse     response;
            string              bashCommand = ToBash(command, args);

            if (shutdown)
            {
                // We're just run commands that shutdown or reboot the server 
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
                        LogLine($"    BINARY OUTPUT [length={response.OutputBinary.Length}]");
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
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Runs a <see cref="CommandBundle"/> with user permissioins on the remote machine.
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
        /// This method is intended for situations where one or more files need to be uploaded to a neonCLUSTER host node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// A good example of this is performing a <b>docker stack</b> command on the cluster.  In this case, we need to
        /// upload the DAB file along with any files it references and then we we'll want to execute the the Docker client.
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
            var response     = RunCommand($"cd {bundleFolder} && ./__run.sh", runOptions | RunOptions.LogBundle);

            response.BashCommand = bundle.ToBash();

            // Remove the bundle files.

            RunCommand($"rm -rf {bundleFolder}", RunOptions.RunWhenFaulted, RunOptions.LogOnErrorOnly);

            return response;
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
        /// Runs a <see cref="CommandBundle"/> under <b>sudo</b> on the remote machine.
        /// </summary>
        /// <param name="bundle">The bundle.</param>
        /// <param name="runOptions">The execution options (defaults to <see cref="RunOptions.Defaults"/>).</param>
        /// <returns>The <see cref="CommandResponse"/>.</returns>
        /// <remarks>
        /// <para>
        /// This method is intended for situations where one or more files need to be uploaded to a neonCLUSTER host node 
        /// and then be used when a command is executed.
        /// </para>
        /// <para>
        /// A good example of this is performing a <b>docker stack</b> command on the cluster.  In this case, we need to
        /// upload the DAB file along with any files it references and then we we'll want to execute the the Docker 
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
        /// <note>
        /// Any <c>null</c> arguments will be ignored.
        /// </note>
        /// </remarks>
        public CommandResponse SudoCommand(CommandBundle bundle, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(bundle != null);

            // Write the START log line here so we can log the actual command being
            // executed and then disable this at the lower level, which would have 
            // logged the execution of the "__run.sh" script.

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
            var response     = SudoCommand($"cd {bundleFolder} && /bin/bash ./__run.sh", runOptions | RunOptions.LogBundle);

            response.BashCommand = bundle.ToBash();

            // Remove the bundle files.

            SudoCommand($"rm -rf {bundleFolder}", runOptions);

            return response;
        }

        /// <summary>
        /// Runs a Docker command on the node under <b>sudo</b> with specific run options
        /// while attempting to handle transient errors.
        /// </summary>
        /// <param name="command">The Linux command.</param>
        /// <param name="runOptions">The execution options.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large clusters.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public CommandResponse DockerCommand(RunOptions runOptions, string command, params object[] args)
        {
            // $todo(jeff.lill): Hardcoding transient error handling for now.

            CommandResponse response    = null;
            int             attempt     = 0;
            int             maxAttempts = 10;
            TimeSpan        delay       = TimeSpan.FromSeconds(15);
            string          orgStatus   = Status;

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

                    Fault(response.ErrorText.Trim());
                    return response;
                }
            }

            LogLine($"*** Operation failed after retrying [{maxAttempts}] times.");
            Fault();

            return response;
        }

        /// <summary>
        /// Runs a Docker command on the node under <b>sudo</b> with <see cref="RunOptions.LogOutput"/>
        /// while attempting to handle transient errors.
        /// </summary>
        /// <param name="command">The Linux command.</param>
        /// <param name="args">The command arguments.</param>
        /// <remarks>
        /// <para>
        /// This method attempts to retry transient Docker client errors (e.g. when an
        /// image pull fails for some reason).  Using this will be more reliable than
        /// executing the command directly, especially on large clusters.
        /// </para>
        /// <note>
        /// You'll need to passes the full Docker command, including the leading
        /// <b>docker</b> client program name.
        /// </note>
        /// </remarks>
        public CommandResponse DockerCommand(string command, params object[] args)
        {
            return DockerCommand(RunOptions.LogOutput, command, args);
        }

        /// <summary>
        /// Invokes a named action on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="name">The node-unique action name.</param>
        /// <param name="action">Tbe action to be performed.</param>
        /// <remarks>
        /// <para>
        /// <paramref name="name"/> must uniquely identify the action on the node.
        /// This name may include letters, digits, and dashes.
        /// </para>
        /// <para>
        /// This method tracks successful action completion by creating a file
        /// on the node at <see cref="NodeHostFolders.State"/><b>/finished-NAME</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public void InvokeIdempotentAction(string name, Action action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(action != null);

            foreach (var ch in name)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '-')
                {
                    throw new ArgumentException($"Idempotent action name [{name}] is invalid because it includes a character that's not a letter, digit, or dash.");
                }
            }

            var statePath = LinuxPath.Combine(NodeHostFolders.State, $"finished-{name}");

            if (!hasStateFolder)
            {
                SudoCommand($"mkdir -p {NodeHostFolders.State}");
                hasStateFolder = true;
            }

            if (FileExists(statePath))
            {
                return;
            }

            action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}");
            }
        }

        /// <summary>
        /// Verifies a TLS/SSL certificate.
        /// </summary>
        /// <param name="name">The certificate name (included in errors).</param>
        /// <param name="certificate">The certificate being tested or <c>null</c>.</param>
        /// <param name="hostName">The host name to be secured by the certificate.</param>
        /// <returns>The command response.</returns>
        /// <remarks>
        /// You may pass <paramref name="certificate"/> as <c>null</c> to indicate that no 
        /// checking is to be performed as a convienence.
        /// </remarks>
        public CommandResponse VerifyCertificate(string name, TlsCertificate certificate, string hostName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            if (certificate == null)
            {
                return new CommandResponse() { ExitCode = 0 };
            }

            Status = $"verifying: [{name}] certificate";

            if (string.IsNullOrEmpty(hostName))
            {
                throw new ArgumentException($"No host name is specified for the [{name}] certificate test.");
            }

            // Verify that the private key looks reasonable.

            if (!certificate.Key.StartsWith("-----BEGIN PRIVATE KEY-----"))
            {
                throw new FormatException($"The [{name}] certificate's private key is not PEM encoded.");
            }

            // Verify the certificate.

            if (!certificate.Cert.StartsWith("-----BEGIN CERTIFICATE-----"))
            {
                throw new ArgumentException($"The [{name}] certificate is not PEM encoded.");
            }

            // We're going to split the certificate into two files, the issued
            // certificate and the certificate authority's certificate chain
            // (AKA the CA bundle).
            //
            // Then we're going to upload these to [/tmp/cert.crt] and [/tmp/cert.ca]
            // and then use the [openssl] command to verify it.

            var pos = certificate.Cert.IndexOf("-----END CERTIFICATE-----");

            if (pos == -1)
            {
                throw new ArgumentNullException($"The [{name}] certificate is not formatted properly.");
            }

            pos = certificate.Cert.IndexOf("-----BEGIN CERTIFICATE-----", pos);

            var issuedCert = certificate.Cert.Substring(0, pos);
            var caBundle   = certificate.Cert.Substring(pos);

            try
            {
                UploadText("/tmp/cert.crt", issuedCert);
                UploadText("/tmp/cert.ca", caBundle);

                return SudoCommand(
                    "openssl verify",
                    RunOptions.FaultOnError,
                    "-verify_hostname", hostName,
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
        /// <exception cref="NeonClusterException">Thrown if the interface was not found.</exception>
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

            throw new NeonClusterException($"Cannot find network interface for [address={address}].");
        }
    }
}
