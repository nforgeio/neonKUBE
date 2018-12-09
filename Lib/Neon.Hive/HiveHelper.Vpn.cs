//-----------------------------------------------------------------------------
// FILE:	    HiveHelper.Vpn.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Net;

namespace Neon.Hive
{
    public static partial class HiveHelper
    {
        //---------------------------------------------------------------------
        // We manage VPN connections by launching OpenVPN specifying a configuration
        // file for the target hive.  These configuration files and keys will be 
        // located in a (hopefully) encrypted or transient [tmpfs] folder.  Each
        // hive configuration file path will look something like:
        //
        //      .../vpn/HIVE/client.conf        *** on Linux
        //      ...\vpn\HIVE\client.conf        *** on Windows
        //
        // where HIVE is the hive name and [...] will vary based on the environment:
        //
        //      1. Windows:         located in the user's [AppData] folder.
        //      2. Linux/OSX:       located in the user's home folder.
        //      3. Tool Container:  located in [/dev/shm].
        //
        // Each connection folder includes the following files:
        //
        //      ca.crt          - Certificate authority's certificate
        //      client.conf     - OpenVPN client configuration
        //      client.crt      - Client certificate
        //      client.key      - Client private key
        //      open.cmd/.sh    - Script to manually start the client (for debugging)
        //      pid             - Client process ID
        //      status.txt      - Status file updated every [VpnStatusSeconds]
        //                        when a connection is established.
        //      ta.key          - Shared TLS HMAC key
        //
        // The code below determines VPN connection status by:
        //
        //      1. List all processes who's name start with "openvpn".
        //
        //      2. Comparing each process ID to the PID files in the VPN
        //         client folders.
        //
        //      3. Processes that match one of the folder PIDs are considered
        //         to be client VPN connections.
        //
        //      4. Connection status is determined by looking at the [status.txt]
        //         file.  This is updated every [VpnStatusInterval] when the
        //         connection is healthy.  The timestamp on the second line is 
        //         is compared to the current time to determine detection health.
        //         If no status file exists, we'll assume that OpenVPN is connecting.
        //
        //      5. VPN client folders with PIDs that don't match one of the 
        //         OpenVPN processes scanned above will be considered closed
        //         and the folder will be deleted.

        private static readonly int VpnStatusSeconds = 10;

        /// <summary>
        /// Enumerates the possible VPN states.
        /// </summary>
        public enum VpnState
        {
            /// <summary>
            /// The VPN client is in the process of connecting to the server.
            /// </summary>
            Connecting,

            /// <summary>
            /// The VPN connection is healthy.
            /// </summary>
            Healthy,

            /// <summary>
            /// The VPN connection is unhealthy.
            /// </summary>
            Unhealthy,
        }

        /// <summary>
        /// Holds information about a VPN client.
        /// </summary>
        public class VpnClient
        {
            /// <summary>
            /// Fully qualified path to the client folder.
            /// </summary>
            public string FolderPath { get; internal set; }

            /// <summary>
            /// The hive name.
            /// </summary>
            public string HiveName { get; internal set; }

            /// <summary>
            /// The connection state.
            /// </summary>
            public VpnState State { get; internal set; }

            /// <summary>
            /// The OpenVPN process ID.
            /// </summary>
            public int Pid { get; internal set; }
        }

        /// <summary>
        /// Returns the folder path where the VPN hive client folders will
        /// be located.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string GetVpnFolder()
        {
            if (HiveHelper.InToolContainer)
            {
                return "/dev/shm/vpn";
            }
            else
            {
                return Path.Combine(HiveHelper.GetHiveUserFolder(), "vpn");
            }
        }

        /// <summary>
        /// Returns current neonHIVE VPN clients.
        /// </summary>
        /// <returns>The <see cref="VpnClient"/> instances.</returns>
        public static List<VpnClient> VpnListClients()
        {
            // Build a hashset of the IDs of the processes that could conceivably
            // be a hive VPN client.

            var openVpnProcessIds = new HashSet<int>();

            foreach (var process in Process.GetProcesses())
            {
                if (process.ProcessName.StartsWith("openvpn", StringComparison.OrdinalIgnoreCase))
                {
                    openVpnProcessIds.Add(process.Id);
                }

                process.Dispose();
            }

            // Scan the VPN client folders.

            var vpnFolder = GetVpnFolder();
            var clients   = new List<VpnClient>();

            Directory.CreateDirectory(vpnFolder);

            foreach (var clientFolder in Directory.GetDirectories(vpnFolder))
            {
                var pidPath    = Path.Combine(clientFolder, "pid");
                var statusPath = Path.Combine(clientFolder, "status.txt");

                // Folders without a [pid] file will be ignored (but left alone).
                // This can happen if the OpenVPN client is in the process of
                // being started.  (This is a bit fragile).

                if (!File.Exists(pidPath))
                {
                    continue;
                }

                // Folders with [pid] files with IDs that are not in the hash
                // set map to VPN clients that are no longer running.  These
                // folders will be deleted and ignored.

                if (!int.TryParse(File.ReadAllText(pidPath), out var pid) || !openVpnProcessIds.Contains(pid))
                {
                    NeonHelper.DeleteFolderContents(clientFolder);
                    continue;
                }

                // We'll extract the hive name from the last directory segment 
                // of the client folder.

                var hiveName = clientFolder.Split('/', '\\').Last();
                
                // Folders that map to a running process but without [status.txt]
                // will have [Connecting] status.

                var state = VpnState.Connecting;

                if (File.Exists(statusPath))
                {
                    // Folders with the [status.txt] timestamp greater or equal to 
                    // [currentTime - 2 * VpnStatusSeconds] are considered healthy.
                    // Older timestamps are considered unhealthy.
                    //
                    // The timestamp is on the second line of [status.txt] which
                    // will look something like:
                    //
                    //      Updated,Wed Mar 22 10:01:30 2017
                    //
                    // Note that it's possible for this operation to fail when
                    // OpenVPN just happens to try to update the status file at
                    // the exact moment we're reading it.  To mitigate this, we're
                    // going to try this up to three times, with a small delay 
                    // between attempts.

                    string timestampLine;

                    for (int tryCount = 1; tryCount <= 3; tryCount++)
                    {
                        try
                        {
                            using (var statusFile = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                using (var statusReader = new StreamReader(statusFile))
                                {
                                    statusReader.ReadLine();
                                    timestampLine = statusReader.ReadLine();
                                }
                            }

                            if (timestampLine == null)
                            {
                                state = VpnState.Connecting;
                                break;
                            }

                            var fields = timestampLine.Split(new char[] { ',' }, 2);

                            if (fields.Length == 2)
                            {
                                var timestamp = DateTime.ParseExact(fields[1], "ddd MMM d HH:mm:ss yyyy", CultureInfo.InvariantCulture);

                                if (timestamp >= DateTime.Now - TimeSpan.FromSeconds(2 * VpnStatusSeconds))
                                {
                                    state = VpnState.Healthy;
                                }
                                else
                                {
                                    state = VpnState.Unhealthy;
                                }
                            }

                            break;
                        }
                        catch
                        {
                            if (tryCount == 3)
                            {
                                throw;
                            }

                            Task.Delay(TimeSpan.FromMilliseconds(50));
                        }
                    }
                }

                clients.Add(
                    new VpnClient()
                    {
                        HiveName   = hiveName,
                        FolderPath = clientFolder,
                        Pid        = pid,
                        State      = state
                    });
            }

            return clients;
        }

        /// <summary>
        /// Returns the path to the client folder a named hive.
        /// </summary>
        /// <param name="hiveName">The hive name.</param>
        /// <returns>The folder path.</returns>
        private static string GetVpnClientFolder(string hiveName)
        {
            return Path.Combine(GetVpnFolder(), hiveName);
        }

        /// <summary>
        /// Determines if a VPN client is running for a hive and returns it.
        /// </summary>
        /// <param name="hiveName">The hive name.</param>
        /// <returns>The <see cref="VpnClient"/> or <c>null</c>.</returns>
        public static VpnClient VpnGetClient(string hiveName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hiveName));

            return VpnListClients().FirstOrDefault(p => p.HiveName.Equals(hiveName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Escapes backslash (\) characters on Windows by adding a second
        /// backslash to each.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The escaped output.</returns>
        private static string EscapeWinBackslash(string input)
        {
            if (NeonHelper.IsWindows)
            {
                return input.Replace("\\", "\\\\");
            }
            else
            {
                return input;
            }
        }

        /// <summary>
        /// Ensures that a hive VPN connection is established and healthy.
        /// </summary>
        /// <param name="hiveLogin">The hive login.</param>
        /// <param name="timeoutSeconds">Maximum seconds to wait for the VPN connection (defaults to 120 seconds).</param>
        /// <param name="onStatus">Optional callback that will be passed a status string.</param>
        /// <param name="onError">Optional callback that will be passed a error string.</param>
        /// <param name="show">
        /// Optionally prints the OpenVPN connection status to the console for connection
        /// debugging purposes.
        /// </param>
        /// <returns><c>true</c> if the connection was established (or has already been established).</returns>
        /// <exception cref="TimeoutException">
        /// Thrown if the VPN connection could not be established before the timeout expired.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown if the VPN connection is unhealthy.
        /// </exception>
        public static void VpnOpen(HiveLogin hiveLogin, int timeoutSeconds = 120, Action<string> onStatus = null, Action<string> onError = null, bool show = false)
        {
            Covenant.Requires<ArgumentNullException>(hiveLogin != null);

            var    vpnClient = VpnGetClient(hiveLogin.HiveName);
            string message;

            if (vpnClient != null)
            {
                if (show)
                {
                    throw new HiveException("A VPN connection already exists for this hive.");
                }

                switch (vpnClient.State)
                {
                    case VpnState.Healthy:

                        return;

                    case VpnState.Unhealthy:

                        message = $"[{hiveLogin.HiveName}] VPN connection is unhealthy.";

                        onError?.Invoke(message);
                        throw new Exception(message);

                    case VpnState.Connecting:

                        onStatus?.Invoke($"Connecting [{hiveLogin.HiveName}] VPN...");

                        try
                        {
                            NeonHelper.WaitFor(
                                () =>
                                {
                                    vpnClient = VpnGetClient(hiveLogin.HiveName);

                                    if (vpnClient != null)
                                    {
                                        return vpnClient.State == VpnState.Healthy;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                },
                                TimeSpan.FromSeconds(timeoutSeconds));
                        }
                        catch (TimeoutException)
                        {
                            throw new TimeoutException($"VPN connection could not be established within [{timeoutSeconds}] seconds.");
                        }

                        return;

                    default:

                        throw new NotImplementedException();
                }
            }

            // Initialize the VPN folder for the hive (deleting any
            // existing folder).

            var clientFolder = GetVpnClientFolder(hiveLogin.HiveName);

            NeonHelper.DeleteFolderContents(clientFolder);
            Directory.CreateDirectory(clientFolder);

            File.WriteAllText(Path.Combine(clientFolder, "ca.crt"), hiveLogin.VpnCredentials.CaCert);
            File.WriteAllText(Path.Combine(clientFolder, "client.crt"), hiveLogin.VpnCredentials.UserCert);
            File.WriteAllText(Path.Combine(clientFolder, "client.key"), hiveLogin.VpnCredentials.UserKey);
            File.WriteAllText(Path.Combine(clientFolder, "ta.key"), hiveLogin.VpnCredentials.TaKey);

            // VPN servers are reached via the manager load balancer or router
            // using the forwarding port rule assigned to each manager node.

            Covenant.Assert(hiveLogin.Definition.Network.ManagerPublicAddress != null, "Manager router address is required.");

            var servers     = string.Empty;
            var firstServer = true;

            foreach (var manager in hiveLogin.Definition.Managers)
            {
                if (firstServer)
                {
                    firstServer = false;
                }
                else
                {
                    servers += "\r\n";
                }

                servers += $"remote {hiveLogin.Definition.Network.ManagerPublicAddress} {manager.VpnFrontendPort}";
            }

            // Generate the client side configuration.

            var config =
$@"##############################################
# Sample client-side OpenVPN 2.0 config file #
# for connecting to multi-client server.     #
#                                            #
# This configuration can be used by multiple #
# clients, however each client should have   #
# its own cert and key files.                #
#                                            #
# On Windows, you might want to rename this  #
# file so it has a .ovpn extension           #
##############################################

# Specify that we are a client and that we
# will be pulling certain config file directives
# from the server.
client

# Use the same setting as you are using on
# the server.
# On most systems, the VPN will not function
# unless you partially or fully disable
# the firewall for the TUN/TAP interface.
;dev tap
dev tun

# Windows needs the TAP-Windows adapter name
# from the Network Connections panel
# if you have more than one.  On XP SP2,
# you may need to disable the firewall
# for the TAP adapter.
;dev-node MyTap

# Are we connecting to a TCP or
# UDP server?  Use the same setting as
# on the server.
proto tcp
;proto udp

# The hostname/IP and port of the server.
# You can have multiple remote entries
# to load balance between the servers.
{servers}

# Choose a random host from the remote
# list for load-balancing.  Otherwise
# try hosts in the order specified.
remote-random

# Keep trying indefinitely to resolve the
# host name of the OpenVPN server.  Very useful
# on machines which are not permanently connected
# to the internet such as laptops.
resolv-retry infinite

# Most clients don't need to bind to
# a specific local port number.
nobind

# Downgrade privileges after initialization (non-Windows only)
;user nobody
;group nobody

# Try to preserve some state across restarts.
;persist-key
;persist-tun

# If you are connecting through an
# HTTP proxy to reach the actual OpenVPN
# server, put the proxy server/IP and
# port number here.  See the man page
# if your proxy server requires
# authentication.
;http-proxy-retry # retry on connection failures
;http-proxy [proxy server] [proxy port #]

# Wireless networks often produce a lot
# of duplicate packets.  Set this flag
# to silence duplicate packet warnings.
;mute-replay-warnings

# SSL/TLS parms.
# See the server config file for more
# description.  It's best to use
# a separate .crt/.key file pair
# for each client.  A single ca
# file can be used for all clients.
ca ""{EscapeWinBackslash(Path.Combine(clientFolder, "ca.crt"))}""
cert ""{EscapeWinBackslash(Path.Combine(clientFolder, "client.crt"))}""
key ""{EscapeWinBackslash(Path.Combine(clientFolder, "client.key"))}""

# Verify server certificate by checking
# that the certicate has the nsCertType
# field set to ""server"".  This is an
# important precaution to protect against
# a potential attack discussed here:
#  http://openvpn.net/howto.html#mitm
#
# To use this feature, you will need to generate
# your server certificates with the nsCertType
# field set to ""server"".  The build-key-server
# script in the easy-rsa folder will do this.
remote-cert-tls server

# If a tls-auth key is used on the server
# then every client must also have the key.
tls-auth ""{EscapeWinBackslash(Path.Combine(clientFolder, "ta.key"))}"" 1

# Select a cryptographic cipher.
# If the cipher option is used on the server
# then you must also specify it here.
cipher AES-256-CBC

# Enable compression on the VPN link.
# Don't enable this unless it is also
# enabled in the server config file.
#
# We're not enabling this due to the
# VORACLE security vulnerablity:
#
#   https://community.openvpn.net/openvpn/wiki/VORACLE
#
#comp-lzo

# Set log file verbosity.
verb 3

# Silence repeating messages
; mute 20
";
            var configPath = Path.Combine(clientFolder, "client.conf");
            var statusPath = Path.Combine(clientFolder, "status.txt");
            var pidPath    = Path.Combine(clientFolder, "pid");

            File.WriteAllText(configPath, config.Replace("\r", string.Empty));  // Linux-style line endings

            // Launch OpenVPN via a script to establish a connection.

            var startInfo = new ProcessStartInfo("openvpn")
            {
                Arguments      = $"--config \"{configPath}\" --status \"{statusPath}\" {VpnStatusSeconds}",
                CreateNoWindow = !show,
            };

            // Write a script for manual debugging VPN purposes.

            var scriptPath = Path.Combine(clientFolder, NeonHelper.IsWindows ? "open.cmd" : "open.sh");

            File.WriteAllText(scriptPath, $"openvpn {startInfo.Arguments}");

            // Add the default OpenVPN installation folder to the PATH
            // environment variable if it's not present already.

            if (NeonHelper.IsWindows)
            {
                var defaultOpenVpnFolder = @"C:\Program Files\OpenVPN\bin";
                var path                 = Environment.GetEnvironmentVariable("PATH");

                if (path.IndexOf(defaultOpenVpnFolder, StringComparison.InvariantCultureIgnoreCase) == -1)
                {
                    Environment.SetEnvironmentVariable("PATH", $"{path};{defaultOpenVpnFolder}");
                }
            }
            else if (NeonHelper.IsOSX)
            {
                throw new NotImplementedException("$todo(jeff.lill): Implement this.");
            }
            else
            {
                throw new NotSupportedException();
            }

            try
            {
                var process = Process.Start(startInfo);

                File.WriteAllText(pidPath, $"{process.Id}");

                // This detaches the OpenVPN process from the current process so OpenVPN
                // will continue running after the current process terminates.

                process.Dispose();
            }
            catch (Exception e)
            {
                NeonHelper.DeleteFolderContents(clientFolder);
                throw new Exception($"*** ERROR: Cannot launch [OpenVPN].  Make sure OpenVPN is installed to its default folder or is on the PATH.", e);
            }

            // Wait for the VPN connection.

            onStatus?.Invoke($"Connecting [{hiveLogin.HiveName}] VPN...");

            // $hack(jeff.lill):
            //
            // Marcus had VPN problems on his workstation when logging into a hive.
            // The VPN would appear to connect but the SSH connection would fail.  The
            // underlying issue turned out be that the Windows-TAP driver wasn't
            // installed and the OpenVPN client failed to start.
            //
            //      https://github.com/jefflill/NeonForge/issues/161
            //
            // I'm not entirely sure why my VPN health checks didn't detect this
            // problem.  I believe this may have occurred because the OpenVPN
            // process hasn't yet terminated when I did the first health check
            // and so it appeared healthy (a race condition).
            //
            // I'm going to hack around this by moving the 10sec delay
            // from further down in this method to here so hopefully OpenVPN
            // will terminate in time to detect the problem.

            Thread.Sleep(TimeSpan.FromSeconds(10));

            // Wait for the VPN to connect.

            vpnClient = VpnGetClient(hiveLogin.HiveName);

            if (vpnClient != null)
            {
                if (vpnClient.State == VpnState.Healthy)
                {
                    onStatus?.Invoke($"VPN is connected");
                    return;
                }
                else if (vpnClient.State == VpnState.Unhealthy)
                {
                    message = $"[{hiveLogin.HiveName}] VPN connection is unhealthy";

                    if (onError != null)
                    {
                        onError(message);
                    }

                    throw new Exception(message);
                }
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        vpnClient = VpnGetClient(hiveLogin.HiveName);

                        if (vpnClient != null)
                        {
                            return vpnClient.State == VpnState.Healthy;
                        }
                        else
                        {
                            return false;
                        }
                    },
                    TimeSpan.FromSeconds(timeoutSeconds));

                onStatus?.Invoke($"Connected to [{hiveLogin.HiveName}] VPN");
                return;
            }
            catch (TimeoutException)
            {
                throw new TimeoutException($"VPN connection could not be established within [{timeoutSeconds}] seconds.");
            }
        }

        /// <summary>
        /// Disconnects a VPN client.
        /// </summary>
        /// <param name="vpnClient">The VPN client.</param>
        private static void VpnDisconnect(VpnClient vpnClient)
        {
            if (vpnClient == null)
            {
                return;
            }

            try
            {
                var process = Process.GetProcessById(vpnClient.Pid);

                process.Kill();
            }
            catch
            {
                // Intentionally ignoring errors.
            }
            finally
            {
                // Remove the VPN files for somewhat better security.

                var clientFolder = GetVpnClientFolder(vpnClient.HiveName);

                NeonHelper.DeleteFolderContents(clientFolder);
            }
        }

        /// <summary>
        /// Disconnects a VPN from a hive or the VPNs for all hives.
        /// </summary>
        /// <param name="hiveName">The target hive name or <c>null</c> if all hives are to be disconnected.</param>
        public static void VpnClose(string hiveName)
        {
            if (hiveName == null)
            {
                foreach (var vpnClient in VpnListClients())
                {
                    VpnDisconnect(vpnClient);
                }
            }
            else
            {
                VpnDisconnect(VpnGetClient(hiveName));
            }
        }
    }
}