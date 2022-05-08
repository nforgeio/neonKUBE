//-----------------------------------------------------------------------------
// FILE:	    LinuxSshProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.SSH
{
    /// <summary>
    /// <para>
    /// Uses a SSH/SCP connection to provide access to Linux machines to access
    /// files, run commands, etc.
    /// </para>
    /// </summary>
    /// <remarks>
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
    public class LinuxSshProxy : IDisposable
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Path to the file whose existence indicates that the proxy has already 
        /// configured things like disabling SUDO password prompts.
        /// </summary>
        public const string SshProxyInitPath = "/etc/neon-sshproxy-init";

        /// <summary>
        /// Used to ensure that only one SSH.NET connection attempt will be inflight
        /// at the same time to the same target computer.
        /// </summary>
        private static Dictionary<string, object> connectLocks = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

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
            // the past because we were only using LinuxSshProxy to establish single connections
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
        // that the remote machine is still rebooting.
        private readonly string RebootStatusPath = $"{HostFolders.Tmpfs}/rebooting";

        private readonly object     syncLock   = new object();
        private bool                isDisposed = false;
        private SshClient           sshClient;
        private ScpClient           scpClient;
        private string              status;
        private bool                isReady;
        private bool                isFaulted;
        private string              faultMessage;

#pragma warning disable 1591
        protected SshCredentials    credentials;
        protected TextWriter        logWriter;
#pragma warning restore 1591

        /// <summary>
        /// Constructs a <see cref="LinuxSshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the remote machine.</param>
        /// <param name="address">The private cluster IP address for the remote machine.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="port">Optionally overrides the standard SSH port (22).</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public LinuxSshProxy(string name, IPAddress address, SshCredentials credentials, int port = NetworkPorts.SSH, TextWriter logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(credentials != null, nameof(credentials));

            this.Name           = name;
            this.Address        = address;
            this.credentials    = credentials;
            this.logWriter      = logWriter;

            this.sshClient      = null;
            this.scpClient      = null;
            this.SshPort        = port;
            this.Status         = string.Empty;
            this.IsReady        = false;
            this.IsFaulted      = false;
            this.faultMessage   = null;
            this.ConnectTimeout = TimeSpan.FromSeconds(5);
            this.FileTimeout    = TimeSpan.FromSeconds(30);
            this.RetryCount     = 10;
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~LinuxSshProxy()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources (e.g. any open remote machine connections).
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources (e.g. any open remote machine connections).
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
                                logWriter.Flush();
                                logWriter.Close();
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
        /// <para>
        /// Returns a clone of the SSH proxy.  This can be useful for situations where you
        /// need to be able to perform multiple SSH/SCP operations against the same
        /// machine in parallel.
        /// </para>
        /// <note>
        /// This does not clone any attached log writer.
        /// </note>
        /// </summary>
        /// <returns>The cloned <see cref="LinuxSshProxy{TMetadata}"/>.</returns>
        public LinuxSshProxy Clone()
        {
            var clone = new LinuxSshProxy(Name, Address, credentials);

            CloneTo(clone);

            return clone;
        }

        /// <summary>
        /// Used by derived classes to copy the base class state to a new
        /// instance as well as configure the new connection's SSH and SCP
        /// clients.
        /// </summary>
        /// <param name="target">The target proxy.</param>
        protected void CloneTo(LinuxSshProxy target)
        {
            Covenant.Requires<ArgumentNullException>(target != null, nameof(target));

            target.Name           = this.Name;
            target.Address        = this.Address;
            target.SshPort        = this.SshPort;
            target.credentials    = this.credentials;
            target.OsName         = this.OsName;
            target.OsVersion      = this.OsVersion;
            target.KernelVersion  = this.KernelVersion;
            target.KernelRelease  = this.KernelRelease;
            target.ConnectTimeout = this.ConnectTimeout;
            target.FileTimeout    = this.FileTimeout;
            target.RetryCount     = this.RetryCount;

            var connectionInfo = this.GetConnectionInfo();

            target.sshClient = new SshClient(connectionInfo);
            target.scpClient = new ScpClient(connectionInfo);
        }

        /// <inheritdoc/>
        public string OsName { get; private set; }

        /// <inheritdoc/>
        public Version OsVersion { get; private set; }

        /// <inheritdoc/>
        public Version KernelVersion { get; private set; }

        /// <inheritdoc/>
        public string KernelRelease { get; private set; }

        /// <inheritdoc/>
        public string Name { get; private set; }

        /// <inheritdoc/>
        public IPAddress Address { get; set; }

        /// <inheritdoc/>
        public int SshPort { get; set; }

        /// <inheritdoc/>
        public TimeSpan ConnectTimeout { get; set; }

        /// <inheritdoc/>
        public TimeSpan FileTimeout { get; set; }

        /// <inheritdoc/>
        public int RetryCount { get; set; }

        /// <inheritdoc/>
        public RunOptions DefaultRunOptions { get; set; } = RunOptions.None;

        /// <inheritdoc/>
        public string RemotePath { get; set; } = $"/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/snap/bin";

        /// <summary>
        /// Returns the username used to log into the remote node.
        /// </summary>
        public string Username => credentials.Username;

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public bool IsInvolved { get; set; }

        /// <inheritdoc/>
        public bool IsConfiguring { get; set; }

        /// <inheritdoc/>
        public bool IsReady
        {
            get { return IsFaulted || isReady; }
            set { isReady = value; }
        }

        /// <inheritdoc/>
        public bool IsFaulted
        {
            get => this.isFaulted;
            set => this.isFaulted = value;
        }

        /// <inheritdoc/>
        public string HomeFolderPath => HostFolders.Home(Username);

        /// <inheritdoc/>
        public string DownloadFolderPath => HostFolders.Download(Username);

        /// <inheritdoc/>
        public string UploadFolderPath => HostFolders.Upload(Username);

        /// <inheritdoc/>
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

            //LogLine($"*** DEADLOCK EXECUTE: {actionName}");

            var thread = NeonHelper.StartThread(action);

            if (!thread.Join(timeout))
            {
                //LogLine($"*** DEADLOCK BREAK: {actionName}");
                //thread.Abort();
                //LogLine($"*** DEADLOCK BREAK COMPLETE: {actionName}");
            }
        }

        /// <inheritdoc/>
        public void UpdateCredentials(SshCredentials newCredentials)
        {
            Covenant.Requires<ArgumentNullException>(newCredentials != null, nameof(newCredentials));

            this.credentials = newCredentials;
        }

        /// <summary>
        /// Extracts the authentication method from SSH credentials.
        /// </summary>
        /// <param name="credentials">The credentials.</param>
        /// <returns>The <see cref="AuthenticationMethod"/>.</returns>
        protected AuthenticationMethod GetAuthenticationMethod(SshCredentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null, nameof(credentials));

            return credentials.AuthenticationMethod;
        }

        /// <inheritdoc/>
        public void DisableSudoPrompt(string password)
        {
            Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

            const string sshProxyInitPath = SshProxyInitPath;
            
            var connectionInfo = GetConnectionInfo();

            if (!FileExists(sshProxyInitPath))
            {
                using (var shellClient = new SshClient(connectionInfo))
                {
                    shellClient.Connect();

                    // We need to make sure [requiretty] is turned off and that [visiblepw] is allowed such
                    // that sudo can execute commands without a password.  We have to do this using a 
                    // TTY shell because the CentOS distribution deployed by XenServer/XCP-ng requires
                    // a TTY by default; bless their hearts :)
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/926
                    //
                    // We're going to quickly do this here using a SSH.NET shell stream and then follow up
                    // with a more definitive config just below.
                    //
                    // I'm not entirely sure why I need the sleep calls below, but it doesn't work
                    // without them.  This delay happens only once, when the remote machine hasn't
                    // been initialized yet.

                    using (var shell = shellClient.CreateShellStream("terminal", 80, 40, 80, 40, 1024))
                    {
                        // Disable SUDO password prompts

                        shell.WriteLine("echo 'Defaults !requiretty' > /etc/sudoers.d/notty");
                        shell.WriteLine("echo 'Defaults visiblepw'  >> /etc/sudoers.d/notty");
                        shell.Flush();
                        Thread.Sleep(500);
                        shell.WriteLine("echo '%sudo    ALL=NOPASSWD: ALL' >> /etc/sudoers.d/nopasswd");
                        shell.Flush();
                        Thread.Sleep(500);

                        // Ensure that the [/home/root] directory exists.

                        shell.WriteLine("mkdir -p /home/root");
                        shell.WriteLine("sudo chmod 751 /home");
                        shell.WriteLine("sudo chmod 751 /home/root");
                        shell.Flush();
                        Thread.Sleep(500);
                    }

                    using (var scpClient = new ScpClient(connectionInfo))
                    {
                        scpClient.Connect();

                        var sudoDisableScript =
$@"
cat <<EOF > {HostFolders.Home(Username)}/sudo-disable-prompt
#!/bin/bash
echo ""%sudo    ALL=NOPASSWD: ALL"" > /etc/sudoers.d/nopasswd
echo ""Defaults    !requiretty""  > /etc/sudoers.d/notty
echo ""Defaults    visiblepw""   >> /etc/sudoers.d/notty

chown root /etc/sudoers.d/*
chmod 440 /etc/sudoers.d/*
EOF

chmod 770 {HostFolders.Home(Username)}/sudo-disable-prompt

cat <<EOF > {HostFolders.Home(Username)}/askpass
#!/bin/bash
echo {password}
EOF
chmod 770 {HostFolders.Home(Username)}/askpass

export SUDO_ASKPASS={HostFolders.Home(Username)}/askpass

sudo -A {HostFolders.Home(Username)}/sudo-disable-prompt
rm {HostFolders.Home(Username)}/sudo-disable-prompt
rm {HostFolders.Home(Username)}/askpass
";
                        using (var stream = new MemoryStream())
                        {
                            stream.Write(Encoding.UTF8.GetBytes(sudoDisableScript.Replace("\r", string.Empty)));
                            stream.Position = 0;

                            scpClient.Upload(stream, $"{HostFolders.Home(Username)}/sudo-disable");
                            shellClient.RunCommand($"chmod 770 {HostFolders.Home(Username)}/sudo-disable");
                        }

                        shellClient.RunCommand($"{HostFolders.Home(Username)}/sudo-disable");
                        shellClient.RunCommand($"rm {HostFolders.Home(Username)}/sudo-disable");

                        // Indicate that we shouldn't perform these operations again on this machine.

                        shellClient.RunCommand($"sudo touch {sshProxyInitPath}");
                    }
                }
            }
        }

        /// <inheritdoc/>
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

            // Give the remote machine a chance to stop.

            Thread.Sleep(TimeSpan.FromSeconds(10));
            Status = "stopped";
        }

        /// <inheritdoc/>
        public void Reboot(bool wait = true)
        {
            Status = "restarting...";

            // We need to be very sure that the remote machine has actually 
            // rebooted and that we're not logging into the same session.
            // Originally, I just waited 10 seconds and assumed that the
            // SSH server (and maybe Linux) would have shutdown by then
            // so all I'd need to do is wait to reconnect.
            //
            // This was fragile and I have encountered situations where
            // SSH server was still running and the remote machine hadn't restarted
            // after 10 seconds so I essentially reconnected to the remote machine
            // with the reboot still pending.
            //
            // To ensure we avoid this, I'm going to do the following:
            //
            //      1. Create a transient file at [/dev/shm/neonssh/rebooting]. 
            //         Since [/dev/shm] is a TMPFS, this file will no longer
            //         exist after a reboot.
            //
            //      2. Command the remote machine to reboot.
            //
            //      3. Loop and attempt to reconnect.  After reconnecting,
            //         verify that the [/dev/shm/neonssh/rebooting] file is no
            //         longer present.  Reboot is complete if it's gone,
            //         otherwise, we need to continue trying.
            //
            //         We're also going to submit a new reboot command every 
            //         10 seconds when [/dev/shm/neonssh/rebooting] is still present
            //         in case the original reboot command was somehow missed
            //         because the reboot command is not retried automatically.
            //  
            //         Note that step #3 is actually taken care of in the
            //         [WaitForBoot()] method.

            try
            {
                SudoCommand($"mkdir -p {HostFolders.Tmpfs} && touch {RebootStatusPath}");
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

            // Give the remote machine a chance to restart.

            Thread.Sleep(TimeSpan.FromSeconds(10));

            if (wait)
            {
                WaitForBoot();
            }
        }

        /// <inheritdoc/>
        public virtual void Log(string text)
        {
            if (logWriter != null)
            {
                logWriter.Write(text);
            }
        }

        /// <inheritdoc/>
        public virtual void LogLine(string text)
        {
            if (logWriter != null)
            {
                logWriter.WriteLine(text);
                LogFlush();
            }
        }

        /// <inheritdoc/>
        public virtual void LogFlush()
        {
            if (logWriter != null)
            {
                logWriter.Flush();
            }
        }

        /// <inheritdoc/>
        public void LogException(Exception e)
        {
            LogLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
            LogLine($"*** STACK:");
            LogLine(e.StackTrace);
        }

        /// <inheritdoc/>
        public void LogException(string message, Exception e)
        {
            LogLine($"*** ERROR: {message}: {NeonHelper.ExceptionError(e)}");
            LogLine($"*** STACK:");
            LogLine(e.StackTrace);
        }

        /// <inheritdoc/>
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
            var address = Address.ToString();
            var port    = SshPort;

            var connectionInfo = new ConnectionInfo(address, port, credentials.Username, credentials.AuthenticationMethod)
            {
                Timeout = ConnectTimeout
            };

            // Ensure that we use a known good encryption mechanism.

            var encryptionName = "aes256-ctr";

            foreach (var disabledEncryption in connectionInfo.Encryptions
                .Where(encryption => encryption.Key != encryptionName)
                .ToList())
            {
                connectionInfo.Encryptions.Remove(disabledEncryption.Key);
            }

            return connectionInfo;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

                        // We need to verify that the [/dev/shm/neonssh/rebooting] file is not present
                        // to ensure that the machine has actually restarted (see [Reboot()]
                        // for more information.

                        var response = sshClient.RunCommand($"if [ -f \"{RebootStatusPath}\" ] ; then exit 0; else exit 1; fi");

                        if (response.ExitStatus != 0)
                        {
                            // [/dev/shm/neonssh/rebooting] file is not present, so we're done.

                            LogLine($"*** WAITFORBOOT: DONE");
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

                        if (timeout == TimeSpan.Zero || operationTimer.HasFired)
                        {
                            throw;
                        }

                        LogLine($"*** WARNING: Wait for boot failed: {NeonHelper.ExceptionError(e)}");
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(5));
            }

            LogLine($"*** WAITFORBOOT: Connected");

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

                        var name  = split[0];
                        var value = split[1];

                        switch (name)
                        {
                            case "NAME":

                                OsName = value.Replace("\"", string.Empty);
                                break;

                            case "VERSION":

                                var version = value.Replace("\"", string.Empty);
                                var pSpace  = version.IndexOf(' ');

                                if (pSpace != -1)
                                {
                                    version = version.Substring(0, pSpace);
                                }

                                OsVersion = new Version(version);
                                break;
                        }
                    }
                }

                // $note(jefflill):
                //
                // Use [uname -r] to obtain the kernel version.  I'm not entirely sure 
                // how this version is formatted.  I'm currently seeing versions like:
                //
                //      5.4.0-66-generic                    <-- Ubuntu 20.04
                //      5.4.72-microsoft-standard-WSL2      <-- WSL2
                //
                // So I'm going to extract the part from the beginning of the version
                // up to (but including) the first dash if present, and then parse 
                // that as the version number.  We'll set v0.0.0 when we can't parse
                // the version.

                var kernelVersion = this.RunCommand("uname -r")
                    .EnsureSuccess()
                    .OutputText
                    .Trim();

                this.KernelRelease = kernelVersion;

                var dashPos = kernelVersion.IndexOf('-');

                if (dashPos != -1)
                {
                    kernelVersion = kernelVersion.Substring(0, dashPos);
                }

                if (Version.TryParse(kernelVersion, out var v))
                {
                    this.KernelVersion = v;
                }
                else
                {
                    this.KernelVersion = new Version();
                }
            }
            catch
            {
                // It is possible for this to fail when the host folders
                // haven't been created yet.

                this.OsName        = "unknown";
                this.OsVersion     = new Version();
                this.KernelVersion = new Version();
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
                    throw new ObjectDisposedException(nameof(LinuxSshProxy));
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
        /// Ensures that a SSH connection has been established.
        /// </summary>
        /// <exception cref="SshConnectionException">Thrown if a connection could not be established.</exception>
        private void EnsureSshConnection()
        {
            lock (syncLock)
            {
                if (isDisposed)
                {
                    throw new ObjectDisposedException(nameof(LinuxSshProxy));
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
                    throw new ObjectDisposedException(nameof(LinuxSshProxy));
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
                    throw new ObjectDisposedException(nameof(LinuxSshProxy));
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

        /// <inheritdoc/>
        public SshClient CloneSshClient()
        {
            return OpenSshConnection();
        }

        /// <inheritdoc/>
        public ScpClient CloneScpClient()
        {
            return OpenScpConnection();
        }

        /// <summary>
        /// <para>
        /// Ensures that the node is configured such that <see cref="LinuxSshProxy{TMetadata}"/> can function properly.
        /// This includes disabling <b>requiretty</b> as well as restricting <b>sudo</b> from requiring passwords
        /// as well as creating the minimum user home folders required by the proxy for executing scripts as well
        /// as uploading and downloading files.
        /// </para>
        /// <para>
        /// This method creates the <see cref="SshProxyInitPath"/> file such that these operations will only
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
            // Ensure that the minimum set of node folders required by [LinuxSshProxy] exist
            // for the current user.  These are all located in the user's home folder
            // so SUDO is not required to create them.

            Status = "prepare: node folders";

            // [~/.neon]

            var folderPath = HostFolders.NeonHome(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neon/download]

            folderPath = HostFolders.Download(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neon/exec]

            folderPath = HostFolders.Exec(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            // [~/.neon/upload]

            folderPath = HostFolders.Upload(Username);
            sshClient.RunCommand($"mkdir -p {folderPath} && chmod 700 {folderPath}");

            //-----------------------------------------------------------------
            // Disable SUDO password prompts if this hasn't already been done for
            // this host.  Note that you must be logged in using username/password 
            // authentication for this to work.
            //
            // NOTE: neonKUBE cloud based images will already have SUDO prompting disabled
            //       as will VM based images for Hyper-V and XenServer and we initialize
            //       the VM images using password authentication, so this will work for
            //       creating the images as well.

            var response = sshClient.RunCommand("sudo -n true");

            if (response.ExitStatus != 0)
            {
                // SUDO password prompting is not disabled yet.
                //
                // We need to obtain the SSH password used to establish the current connection.  This means
                // that SSH public key based credentials won't work for the first connection to a host.
                // We're going use reflection to get the password from SSH.NET.

                var authMethod = credentials.AuthenticationMethod as PasswordAuthenticationMethod;

                if (authMethod == null)
                {
                    throw new SshProxyException("You must use password credentials the first time you connect to a particular host machine.");
                }

                var passwordProperty = authMethod.GetType().GetProperty("Password", BindingFlags.Instance | BindingFlags.NonPublic);
                var passwordBytes    = (byte[])passwordProperty.GetValue(authMethod);
                var sshPassword      = Encoding.UTF8.GetString(passwordBytes);

                DisableSudoPrompt(sshPassword);
            }
        }

        /// <inheritdoc/>
        public void RemoveFile(string target)
        {
            var response = SudoCommand($"if [ -f \"{target}\" ] ; then rm \"{target}\" ; fi");

            if (response.ExitCode != 0)
            {
                throw new SshProxyException(response.ErrorSummary);
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public byte[] DownloadBytes(string source)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));

            using (var ms = new MemoryStream())
            {
                Download(source, ms);

                return ms.ToArray();
            }
        }

        /// <inheritdoc/>
        public string DownloadText(string source)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(source), nameof(source));

            using (var ms = new MemoryStream())
            {
                Download(source, ms);

                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string path)
        {
            var response = SudoCommand($"if [ -d \"{path}\" ] ; then exit 0; else exit 1; fi");

            // $todo(jefflill):
            //
            // This doesn't really handle the case where the operation fails
            // due to a permissions restriction.

            return response.ExitCode == 0;
        }

        /// <inheritdoc/>
        public bool FileExists(string path)
        {
            var response = SudoCommand($"if [ -f \"{path}\" ] ; then exit 0; else exit 1; fi", RunOptions.None);

            // $todo(jefflill):
            //
            // This doesn't really handle the case where the operation fails
            // due to a permissions restriction.

            return response.ExitCode == 0;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void UploadText(string target, string text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null, nameof(text));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            using (var textStream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                UploadText(target, textStream, tabStop, Encoding.UTF8, outputEncoding, permissions: permissions, owner: owner);
            }
        }

        /// <inheritdoc/>
        public void UploadText(string target, StringBuilder text, int tabStop = 0, Encoding outputEncoding = null, string permissions = null, string owner = null)
        {
            Covenant.Requires<ArgumentNullException>(text != null, nameof(text));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(target), nameof(target));

            UploadText(target, text.ToString(), tabStop: tabStop, outputEncoding: outputEncoding, permissions: permissions, owner: owner);
        }

        /// <inheritdoc/>
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

                var bundleFolder = $"{HostFolders.Exec(Username)}/{Guid.NewGuid().ToString("d")}";
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

        /// <inheritdoc/>
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
            // We're going to use the [~/.neon/exec] folder coordinate
            // this by:
            //
            //      1. Generating a GUID for the operation.
            //
            //      2. Creating a folder named [~/.neon/exec] for the 
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
            // In addition, the [neon-cleaner] service deployed to the cluster nodes will
            // periodically purge orphaned temporary command folders older than one day.

            // Create the command folder.

            var execFolder = $"{HostFolders.Exec(Username)}";
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

        /// <inheritdoc/>
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

                        if (!redact)
                        {
                            message = $"{message}\r\n{response.AllText}";
                        }

                        throw new RemoteCommandException($"[exitcode={response.ExitCode}]: {message}");
                    }
                }
            }

            return response;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public CommandResponse SudoCommand(string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            return SudoCommand(command, DefaultRunOptions, args);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public CommandResponse SudoCommandAsUser(string user, string command, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user), nameof(user));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            return SudoCommandAsUser(user, command, DefaultRunOptions, args);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public ShellStream CreateShell()
        {
            EnsureSshConnection();

            return sshClient.CreateShellStream("dumb", 80, 24, 800, 600, 1024);
        }

        /// <inheritdoc/>
        public ShellStream CreateSudoShell()
        {
            var shell = CreateShell();

            shell.WriteLine("sudo");

            return shell;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public DateTime GetTimeUtc()
        {
            var response = SudoCommand("date +%s", RunOptions.None);

            response.EnsureSuccess();

            var epochSeconds = long.Parse(response.OutputText.Trim());

            return NeonHelper.UnixEpoch + TimeSpan.FromSeconds(epochSeconds);
        }

        /// <inheritdoc/>
        public Dictionary<string, LinuxDiskInfo> ListDisks(bool fixedDisksOnly = true)
        {
            var nameToDisk = new Dictionary<string, LinuxDiskInfo>();

            // We're going to use the [lsblk --json -b] command to list the block
            // devices as JSON with sizes in bytes.  The result will look something
            // like this:
            //     
            //     {
            //         "blockdevices": [
            //           {"name":"fd0", "maj:min":"2:0", "rm":true, "size":4096, "ro":false, "type":"disk", "mountpoint":null},
            //           {"name":"loop0", "maj:min":"7:0", "rm":false, "size":57614336, "ro":true, "type":"loop", "mountpoint":"/snap/core18/1705"},
            //           {"name":"loop1", "maj:min":"7:1", "rm":false, "size":28405760, "ro":true, "type":"loop", "mountpoint":"/snap/snapd/7264"},
            //           {"name":"loop2", "maj:min":"7:2", "rm":false, "size":72318976, "ro":true, "type":"loop", "mountpoint":"/snap/lxd/14804"},
            //           {"name":"loop3", "maj:min":"7:3", "rm":false, "size":58007552, "ro":true, "type":"loop", "mountpoint":"/snap/core18/1885"},
            //           {"name":"loop4", "maj:min":"7:4", "rm":false, "size":31735808, "ro":true, "type":"loop", "mountpoint":"/snap/snapd/9279"},
            //           {"name":"loop5", "maj:min":"7:5", "rm":false, "size":71921664, "ro":true, "type":"loop", "mountpoint":"/snap/lxd/17320"},
            //           {"name":"sda", "maj:min":"8:0", "rm":false, "size":10737418240, "ro":false, "type":"disk", "mountpoint":null,
            //              "children": [
            //                 {"name":"sda1", "maj:min":"8:1", "rm":false, "size":1048576, "ro":false, "type":"part", "mountpoint":null},
            //                 {"name":"sda2", "maj:min":"8:2", "rm":false, "size":10734272512, "ro":false, "type":"part", "mountpoint":"/"}
            //              ]
            //           },
            //           {"name":"sr0", "maj:min":"11:0", "rm":true, "size":1073741312, "ro":false, "type":"rom", "mountpoint":null}
            //        ]
            //     }

            var response    = SudoCommand("lsblk --json -b").EnsureSuccess();
            var rootObject  = JObject.Parse(response.OutputText);
            var deviceArray = (JArray)rootObject.Property("blockdevices").Value;

            foreach (var device in deviceArray.Select(item => (JObject)(item)))
            {
                var deviceName  = device.Property("name").ToObject<string>();
                var isRemovable = device.Property("rm").ToObject<bool>();
                var size        = device.Property("size").ToObject<long>();
                var isReadOnly  = device.Property("ro").ToObject<bool>();
                var type        = device.Property("type").ToObject<string>();

                if (type != "disk" || (fixedDisksOnly && isRemovable))
                {
                    continue;
                }

                deviceName = $"/dev/{deviceName}";

                var partitions   = new List<LinuxDiskPartition>();
                var partitionNum = 1;
                var children     = device.Property("children");

                if (children != null)
                {
                    foreach (var partition in children
                        .Value
                        .ToObject<JArray>()
                        .Select(item => (JObject)item))
                    {
                        var partitionName = partition.Property("name").ToObject<string>();
                        var partitionSize = partition.Property("size").ToObject<long>();
                        var partitionType = partition.Property("type").ToObject<string>();
                        var mountPoint    = partition.Property("mountpoint").ToObject<string>();

                        if (partitionType != "part")
                        {
                            continue;
                        }

                        partitions.Add(new LinuxDiskPartition(partitionNum++, $"/dev/{partitionName}", partitionSize, mountPoint));
                    }
                }

                nameToDisk.Add(deviceName, new LinuxDiskInfo(deviceName, size, isRemovable, isReadOnly, partitions));
            }

            return nameToDisk;
        }

        /// <inheritdoc/>
        public List<string> ListUnpartitionedDisks()
        {
            return ListDisks()
                .Where(item => item.Value.Partitions.Count == 0)
                .Select(item => item.Key)
                .ToList();
        }

        /// <inheritdoc/>
        public List<string> ListPartitionedDisks()
        {
            return ListDisks()
                .Where(item => item.Value.Partitions.Count != 0)
                .Select(item => item.Key)
                .ToList();
        }

        /// <inheritdoc/>
        public CommandResponse KubectlApply(string yaml, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yaml), nameof(yaml));

            var bundle = new CommandBundle("kubectl apply -f file.yaml");

            bundle.AddFile("file.yaml", yaml);

            return SudoCommand(bundle, runOptions);
        }

        /// <inheritdoc/>
        public CommandResponse KubeCtlApply(StringBuilder sbYaml, RunOptions runOptions = RunOptions.Defaults)
        {
            Covenant.Requires<ArgumentNullException>(sbYaml != null, nameof(sbYaml));

            return KubectlApply(sbYaml.ToString(), runOptions);
        }

        /// <inheritdoc/>
        public bool GetNeonInitStatus()
        {
            return FileExists("/etc/neon-init/ready");
        }

        /// <inheritdoc/>
        public void SetNeonInitStatus(bool initialized, bool keepNetworkSettings = false)
        {
            if (initialized)
            {
                var setScript =
@"
set -euo pipefail

mkdir -p /etc/neon-init
touch /etc/neon-init/ready
";
                SudoCommand(CommandBundle.FromScript(setScript));
            }
            else
            {
                var resetScript =
@"
set -euo pipefail

mkdir -p /etc/neon-init
rm -rf /etc/neon-init/*
";
                SudoCommand(CommandBundle.FromScript(resetScript));
            }
            
            if (!keepNetworkSettings)
            {
                // We need to delete the [/etc/neon-init/ready] file and re-enable
                // network DHCP by restoring the original network configuration
                // (if present).

                var resetScript =
@"
mkdir -p /etc/neon-init

rm -rf /etc/netplan/*

if [ -d /etc/neon-init/netplan-backup ]; then
    cp -r /etc/neon-init/netplan-backup/* /etc/netplan
    rm -r /etc/neon-init/netplan-backup
fi

rm -rf /etc/neon-init/*
";
                SudoCommand(CommandBundle.FromScript(resetScript));
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Name;
        }
    }
}
