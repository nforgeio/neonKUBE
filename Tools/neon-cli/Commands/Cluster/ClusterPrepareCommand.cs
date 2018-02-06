//-----------------------------------------------------------------------------
// FILE:	    ClusterPrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster prepare</b> command.
    /// </summary>
    public class ClusterPrepareCommand : CommandBase
    {
        private const string usage = @"
Configures cloud platform virtual machines so that they are prepared 
to host a neonCLUSTER.

USAGE:

    neon prepare cluster [OPTIONS] CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF     - Path to the cluster definition file

OPTIONS:

    --force                     - Optionally specifies that existing resources
                                  (such as virtual machines) are to be 
                                  overwritten or replaced.  The actual 
                                  interpretation of this is specific to 
                                  the hosting enviroment.  This defaults
                                  to [false].

    --package-cache=CACHE-URI   - Optionally specifies an APT Package cache
                                  server to improve setup performance.

    --unredacted                - Runs Vault commands without redacting logs.
                                  This is useful for debugging cluster setup 
                                  issues.  Do not use for production clusters.

Server Requirements:
--------------------

    * Supported version of Linux (server)
    * Known root SSH credentials
    * OpenSSH installed (or another SSH server)
    * [sudo] elevates permissions without a password
";
        private ClusterProxy    cluster;
        private HostingManager  hostingManager;
        private string          clusterDefPath;
        private string          packageCacheUri;
        private VpnCaFiles      vpnCaFiles;
        private VpnCredentials  vpnCredentials;
        private bool            force;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "prepare" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--force", "--package-cache", "--unredacted" }; }
        }

        /// <inheritdoc/>
        public override bool NeedsSshCredentials(CommandLine commandLine)
        {
            return true;
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }
        
        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            packageCacheUri = commandLine.GetOption("--package-cache");     // This overrides the cluster definition, if specified.

            if (Program.ClusterLogin != null)
            {
                Console.Error.WriteLine("*** ERROR: You are logged into a cluster.  You need to logout before preparing another.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine($"*** ERROR: CLUSTER-DEF expected.");
                Program.Exit(1);
            }

            clusterDefPath = commandLine.Arguments[0];
            force          = commandLine.GetFlag("--force");

            ClusterDefinition.ValidateFile(clusterDefPath);

            cluster = new ClusterProxy(ClusterDefinition.FromFile(clusterDefPath), Program.CreateNodeProxy<NodeDefinition>, RunOptions.LogOutput | RunOptions.FaultOnError);

            if (File.Exists(Program.GetClusterLoginPath(NeonClusterConst.RootUser, cluster.Definition.Name)))
            {
                Console.Error.WriteLine($"*** ERROR: A cluster login named [{NeonClusterConst.RootUser}@{cluster.Definition.Name}] already exists.");
                Program.Exit(1);
            }

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.VaultRunOptions = RunOptions.None;
            }

            // Assign the VPN client return subnets to the manager nodes if VPN is enabled.

            if (cluster.Definition.Vpn.Enabled)
            {
                var vpnSubnet            = NetworkCidr.Parse(cluster.Definition.Network.VpnPoolSubnet);
                var prefixLength         = 25;
                var nextVpnSubnetAddress = vpnSubnet.Address;

                // Note that we're not going to assign the first block of addresses in the
                // VPN subnet to any managers to prevent conflicts with addresses reserved
                // by some cloud platforms at the beginning of a subnet.  Azure for example
                // reserves 4 IP addresses for DNS servers and platform provided VPNs.

                foreach (var manager in cluster.Definition.SortedManagers)
                {
                    var managerVpnSubnet    = new NetworkCidr(NetHelper.AddressIncrement(nextVpnSubnetAddress, VpnOptions.ServerAddressCount), prefixLength);

                    manager.VpnPoolSubnet = managerVpnSubnet.ToString();
                    nextVpnSubnetAddress  = managerVpnSubnet.NextAddress;
                }
            }

            //-----------------------------------------------------------------
            // Try to ensure that no servers are already deployed on the IP addresses defined
            // for cluster nodes, because provisoning over an existing cluster will likely
            // corrupt the existing cluster and probably prevent the new cluster from
            // provisioning correctly.
            //
            // Note that we're not going to perform this check for the [Machine] hosting 
            // environment because we're expecting the bare machines to be already running 
            // with the assigned addresses.

            if (cluster.Definition.Hosting.Environment != HostingEnvironments.Machine)
            {
                Console.WriteLine();
                Console.WriteLine("Verify that cluster IP addresses don't conflict with existing machines...");
                Console.WriteLine();

                var pingOptions   = new PingOptions(ttl: 32, dontFragment: true);
                var pingTimeout   = TimeSpan.FromSeconds(2);
                var pingConflicts = new List<NodeDefinition>();
                var pingAttempts  = 2;

                // I'm going to use up to 20 threads at a time here for simplicity
                // rather then doing this as async operations.

                var parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = 20
                };

                Parallel.ForEach(cluster.Definition.NodeDefinitions.Values, parallelOptions,
                    node =>
                    {
                        using (var pinger = new Ping())
                        {
                            // We're going to try pinging up to [pingAttempts] times for each node
                            // just in case the network it sketchy and we're loosing reply packets.

                            for (int i = 0; i < pingAttempts; i++)
                            {
                                var reply = pinger.Send(node.PrivateAddress, (int)pingTimeout.TotalMilliseconds);

                                if (reply.Status == IPStatus.Success)
                                {
                                    lock (pingConflicts)
                                    {
                                        pingConflicts.Add(node);
                                    }

                                    break;
                                }
                            }
                        }
                    });

                if (pingConflicts.Count > 0)
                {
                    Console.Error.WriteLine($"*** ERROR: Cannot provision the cluster because [{pingConflicts.Count}] other machines conflict with the");
                    Console.Error.WriteLine($"***        following cluster nodes:");
                    Console.Error.WriteLine();

                    foreach (var node in pingConflicts.OrderBy(n => NetHelper.AddressToUint(IPAddress.Parse(n.PrivateAddress))))
                    {
                        Console.Error.WriteLine($"{node.PrivateAddress, 16}:    {node.Name}");
                    }

                    Program.Exit(1);
                }
            }

            //-----------------------------------------------------------------
            // Perform basic environment provisioning.  This creates basic cluster components
            // such as virtual machines, networks, load balancers, public IP addresses, security
            // groups,... as required for the environment.

            hostingManager              = HostingManager.GetManager(cluster.Definition.Hosting.Environment, cluster, Program.LogPath);
            hostingManager.HostUsername = Program.Username;
            hostingManager.HostPassword = Program.Password;
            hostingManager.ShowStatus   = !Program.Quiet;
            hostingManager.MaxParallel  = Program.MaxParallel;
            hostingManager.WaitSeconds  = Program.WaitSeconds;

            if (!hostingManager.Provision(force))
            {
                Program.Exit(1);
            }

            // Ensure that the nodes have valid IP addresses.

            cluster.Definition.ValidatePrivateNodeAddresses();

            var ipAddressToServer = new Dictionary<IPAddress, SshProxy<NodeDefinition>>();

            foreach (var node in cluster.Nodes.OrderBy(n => n.Name))
            {
                SshProxy<NodeDefinition> duplicateServer;

                if (node.PrivateAddress == IPAddress.Any)
                {
                    throw new ArgumentException($"Node [{node.Name}] has not been assigned an IP address.");
                }

                if (ipAddressToServer.TryGetValue(node.PrivateAddress, out duplicateServer))
                {
                    throw new ArgumentException($"Nodes [{duplicateServer.Name}] and [{node.Name}] have the same IP address [{node.Metadata.PrivateAddress}].");
                }

                ipAddressToServer.Add(node.PrivateAddress, node);
            }

            //-----------------------------------------------------------------
            // Perform basic node provisioning including operating system updates & configuration,
            // and configure OpenVPN on the manager nodes so that cluster setup will be
            // able to reach the nodes on all ports.

            var operation = $"Preparing [{cluster.Definition.Name}] nodes";

            var controller = 
                new SetupController<NodeDefinition>(operation, cluster.Nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            if (!string.IsNullOrEmpty(packageCacheUri))
            {
                cluster.Definition.PackageProxy = packageCacheUri;
            }

            // Prepare the nodes.

            controller.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
            hostingManager.AddPostProvisionSteps(controller);
            controller.AddStep("verify OS", n => CommonSteps.VerifyOS(n));
            controller.AddStep("prepare", server => CommonSteps.PrepareNode(server, cluster.Definition, shutdown: false));

            // Add any VPN configuration steps.

            if (cluster.Definition.Vpn.Enabled)
            {
                controller.AddGlobalStep("vpn credentials", () => CreateVpnCredentials());
                controller.AddStep("vpn server", n => ConfigManagerVpn(n), n => n.Metadata.IsManager);

                // Add a step to establish a VPN connection if we're provisioning to a cloud.
                // We specifically don't want to do this if we're provisioning to a on-premise
                // datacenter because we're assuming that we're already directly connected to
                // the LAN while preparing and setting up the cluster.

                if (cluster.Definition.Hosting.IsCloudProvider)
                {
                    controller.AddStep("vpn connect",
                        manager =>
                        {
                            // Create a cluster login with just enough credentials to connect the VPN.
                            // Note that this isn't really a node specific command but I wanted to 
                            // be able to display the connection status somewhere.

                            var vpnLogin =
                                    new ClusterLogin()
                                    {
                                        Definition     = cluster.Definition,
                                        VpnCredentials = vpnCredentials
                                    };

                            // Ensure that we don't have an old VPN client for the cluster running.

                            NeonClusterHelper.VpnClose(vpnLogin.Definition.Name);

                            // ...and then start a new one.

                            NeonClusterHelper.VpnOpen(vpnLogin,
                                    onStatus: message => manager.Status = $"{message}",
                                    onError: message => manager.Status = $"ERROR: {message}");
                        },
                        n => n == cluster.FirstManager);
                }

                // Perform any post-VPN setup provisioning required by the hosting provider.

                hostingManager.AddPostVpnSteps(controller);
            }
            
            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Write the cluster login file.

            var clusterLoginPath = Program.GetClusterLoginPath(NeonClusterConst.RootUser, cluster.Definition.Name);
            var clusterLogin     = new ClusterLogin()
            {
                Path         = clusterLoginPath,
                Username     = NeonClusterConst.RootUser,
                Definition   = cluster.Definition,
                SshUsername  = Program.Username,
                SshPassword  = Program.Password,
                SetupPending = true
            };

            if (cluster.Definition.Vpn.Enabled)
            {
                clusterLogin.VpnCredentials = vpnCredentials;
            }

            clusterLogin.Save();
        }

        /// <summary>
        /// Initializes the VPN certificate authority as well as the server and root user's certificates and keys.
        /// </summary>
        private void CreateVpnCredentials()
        {
            // This is a bit tricky: We're going to invoke the [neon vpn ca ...] command to
            // initialize the cluster's certificate authority files.  This command must be
            // run in the [neon-cli] container so we need to detect whether we're already
            // running in the tool container and do the right thing.

            string tempCaFolder;

            if (NeonClusterHelper.InToolContainer)
            {
                tempCaFolder = "/shim/ca";
            }
            else
            {
                tempCaFolder = Path.Combine(Program.ClusterTempFolder, Guid.NewGuid().ToString("D"));
            }

            try
            {
                Directory.CreateDirectory(tempCaFolder);

                // Execute the [neon vpn cert init ...] command to generate the certificate
                // authority files and the root client certificate and key and then load
                // the files into a [VpnCaFiles] object.

                var neonExecutable = NeonHelper.IsWindows ? "neon.cmd" : "neon";

                if (NeonClusterHelper.InToolContainer)
                {
                    Program.Execute(neonExecutable, "--no-tool-container", "vpn", "ca", clusterDefPath, tempCaFolder);
                }
                else
                {
                    Program.Execute(neonExecutable, "vpn", "ca", clusterDefPath, tempCaFolder);
                }

                vpnCaFiles = VpnCaFiles.LoadFolder(tempCaFolder);
            }
            finally
            {
                if (Directory.Exists(tempCaFolder))
                {
                    Directory.Delete(tempCaFolder, recursive: true);
                }
            }
        }

        /// <summary>
        /// Configures OpenVPN on a manager node.
        /// </summary>
        /// <param name="manager">The manager.</param>
        private void ConfigManagerVpn(SshProxy<NodeDefinition> manager)
        {
            // Upload the setup and configuration files.
            //
            // NOTE: 
            //
            // These steps are redundant and will be repeated during the
            // common node configuration, but we need some of the scripts
            // here, before that happens.

            manager.InitializeNeonFolders();
            manager.UploadConfigFiles(cluster.Definition);
            manager.UploadTools(cluster.Definition);

            // Install OpenVPN.

            manager.Status = "vpn install";
            manager.SudoCommand("apt-get update");
            manager.SudoCommand("apt-get install -yq openvpn");

            // Configure OpenVPN.

            var nodesSubnet      = NetworkCidr.Parse(cluster.Definition.Network.NodesSubnet);
            var vpnSubnet        = NetworkCidr.Parse(manager.Metadata.VpnPoolSubnet);
            var duplicateCN      = cluster.Definition.Vpn.AllowSharedCredentials ? "duplicate-cn" : ";duplicate-cn";
            var vpnServerAddress = NetHelper.UintToAddress(NetHelper.AddressToUint(vpnSubnet.Address) + 1);

            var serverConf =
$@"#------------------------------------------------------------------------------
# OpenVPN config file customized for the [{manager.Name}] neonCLUSTER manager node.

# OpenVPN listening port.
port {NetworkPorts.OpenVPN}

# Enable TCP and/or UDP transports.
proto tcp
;proto udp

# Set packet tunneling mode.
dev tun

# SSL/TLS root certificate (ca), certificate
# (cert), and private key (key).  Each client
# and the server must have their own cert and
# key file.  The server and all clients will
# use the same ca file.
#
# See the [easy-rsa] directory for a series
# of scripts for generating RSA certificates
# and private keys.  Remember to use
# a unique Common Name for the server
# and each of the client certificates.
#
# Any X509 key management system can be used.
# OpenVPN can also use a PKCS #12 formatted key file
# (see [pkcs12] directive in man page).
ca ca.crt
cert server.crt
key server.key  # This file should be kept secret

# Diffie hellman parameters (2048-bit) generated via:
# 
#   openssl dhparam -out dhparam.pem 2048
# 
dh dhparam.pem

# The currently recommended topology.
topology subnet

# Configure server mode and supply a VPN subnet
# for OpenVPN to draw client addresses from.
# The server will take {vpnServerAddress} for itself,
# the rest will be made available to clients.
# Each client will be able to reach the server
# on {vpnServerAddress}. Comment this line out if you are
# ethernet bridging. See the man page for more info.
server {vpnSubnet.Address} {vpnSubnet.Mask}

# Maintain a record of client  virtual IP address
# associations in this file.  If OpenVPN goes down or
# is restarted, reconnecting clients can be assigned
# the same virtual IP address from the pool that was
# previously assigned.
;ifconfig-pool-persist ipp.txt

# Push routes to the client to allow it
# to reach other private subnets behind
# the server.  Remember that these
# private subnets will also need
# to know to route the OpenVPN client
# address pool ({vpnSubnet.Address})
# back to this specific OpenVPN server.
push ""route {nodesSubnet.Address} {nodesSubnet.Mask}""

# Uncomment this directive if multiple clients
# might connect with the same certificate/key
# files or common names.  This is recommended
# only for testing purposes.  For production use,
# each client should have its own certificate/key
# pair.
{duplicateCN}

# The keepalive directive causes ping-like
# messages to be sent back and forth over
# the link so that each side knows when
# the other side has gone down.
# Ping every 10 seconds, assume that remote
# peer is down if no ping received during
# a 120 second time period.
keepalive 10 120

# For extra security beyond that provided
# by SSL/TLS, create an [HMAC firewall]
# to help block DoS attacks and UDP port flooding.
#
# Generate with:
#   openvpn --genkey --secret ta.key
#
# The server and each client must have
# a copy of this key.
# The second parameter should be '0'
# on the server and '1' on the clients.
tls-auth ta.key 0 # This file is secret

# Select a cryptographic cipher.
# This config item must be copied to
# the client config file as well.
cipher AES-256-CBC 

# Enable compression on the VPN link.
# If you enable it here, you must also
# enable it in the client config file.
comp-lzo

# The maximum number of concurrently connected
# clients we want to allow.
max-clients {VpnOptions.ServerAddressCount - 2}

# This macro sets the TCP_NODELAY socket flag on 
# the server as well as pushes it to connecting
# clients. The TCP_NODELAY flag disables the Nagle
# algorithm on TCP sockets causing packets to be
# transmitted immediately with low latency, rather
# than waiting a short period of time in order to 
# aggregate several packets into a larger containing
# packet. In VPN applications over TCP, TCP_NODELAY
# is generally a good latency optimization.
tcp-nodelay

# It's a good idea to reduce the OpenVPN
# daemon's privileges after initialization.
#
# You can uncomment this out on
# non-Windows systems.
;user nobody
;group nobody

# The persist options will try to avoid
# accessing certain resources on restart
# that may no longer be accessible because
# of the privilege downgrade.
persist-key
persist-tun

# Output a short status file showing
# current connections, truncated
# and rewritten every minute.
status openvpn-status.log

# By default, log messages will go to the syslog (ork
# on Windows, if running as a service, they will go to
# the [\Program Files\OpenVPN\log] directory).
# Use log or log-append to override this default.
# [log] will truncate the log file on OpenVPN startup,
# while [log-append] will append to it.  Use one
# or the other (but not both).
log         /var/log/openvpn.log
;log-append  openvpn.log

# Set the appropriate level of log
# file verbosity.
#
# 0 is silent, except for fatal errors
# 4 is reasonable for general usage
# 5 and 6 can help to debug connection problems
# 9 is extremely verbose
verb 4

# Silence repeating messages.  At most 20
# sequential messages of the same message
# category will be output to the log.
;mute 20
";
            manager.Status = "vpn config";
            manager.SudoCommand("mkdir -p /etc/openvpn");
            manager.UploadText("/etc/openvpn/server.conf", serverConf);

            manager.UploadText("/etc/openvpn/ca.crt", vpnCaFiles.GetCert("ca"));
            manager.UploadText("/etc/openvpn/server.crt", vpnCaFiles.GetCert("server"));
            manager.UploadText("/etc/openvpn/server.key", vpnCaFiles.GetKey("server"));
            manager.SudoCommand("chmod 600", "/etc/openvpn/server.key");    // This is a secret!

            manager.UploadText("/etc/openvpn/ta.key", vpnCaFiles.GetTaKey());
            manager.SudoCommand("chmod 600", "/etc/openvpn/ta.key");        // This is a secret too!

            manager.UploadText("/etc/openvpn/dhparam.pem", vpnCaFiles.GetDHParam());

            // Initialize the [root] user's credentials.

            vpnCredentials =
                new VpnCredentials()
                {
                    CaCert   = vpnCaFiles.GetCert("ca"),
                    UserCert = vpnCaFiles.GetCert(NeonClusterConst.RootUser),
                    UserKey  = vpnCaFiles.GetKey(NeonClusterConst.RootUser),
                    TaKey    = vpnCaFiles.GetTaKey(),
                    CaZipKey = VpnCaFiles.GenerateKey(),
                    CaZip    = vpnCaFiles.ToZipBytes()
                };

            // Upload the initial (empty) Certificate Revocation List (CRL) file and then 
            // overwrite the OpenVPN systemd file so that it will recognize revoked certificates.

            manager.UploadText("/etc/openvpn/crl.pem", vpnCaFiles.GetFile("crl.pem"));
            manager.SudoCommand("chmod 664", "/etc/openvpn/crl.pem");    // OpenVPN needs to be able to read this after having its privileges downgraded.

            var openVpnUnit =
@"[Unit]
Description=OpenVPN connection to %i
PartOf=openvpn.service
ReloadPropagatedFrom=openvpn.service
Before=systemd-user-sessions.service
Documentation=man:openvpn(8)
Documentation=https://community.openvpn.net/openvpn/wiki/Openvpn23ManPage
Documentation=https://community.openvpn.net/openvpn/wiki/HOWTO

[Service]
PrivateTmp=true
KillMode=mixed
Type=forking
ExecStart=/usr/sbin/openvpn --daemon ovpn-%i --status /run/openvpn/%i.status 10 --cd /etc/openvpn --script-security 2 --config /etc/openvpn/%i.conf --writepid /run/openvpn/%i.pid --crl-verify /etc/openvpn/crl.pem
PIDFile=/run/openvpn/%i.pid
ExecReload=/bin/kill -HUP $MAINPID
WorkingDirectory=/etc/openvpn
ProtectSystem=yes
CapabilityBoundingSet=CAP_IPC_LOCK CAP_NET_ADMIN CAP_NET_BIND_SERVICE CAP_NET_RAW CAP_SETGID CAP_SETUID CAP_SYS_CHROOT CAP_DAC_READ_SEARCH CAP_AUDIT_WRITE
LimitNPROC=10
DeviceAllow=/dev/null rw
DeviceAllow=/dev/net/tun rw

[Install]
WantedBy=multi-user.target
";
            manager.UploadText("/lib/systemd/system/openvpn@.service", openVpnUnit);
            manager.SudoCommand("chmod 644 /lib/systemd/system/openvpn@.service");

            // Enable and restart OpenVPN.

            manager.SudoCommand("systemctl enable openvpn");
            manager.SudoCommand("systemctl restart openvpn");

            //-----------------------------------------------------------------
            // SPECIAL NOTE:
            //
            // I figured out that I need this lovely bit of code after banging my head on the desk for
            // 12 freaking days.  The problem was getting OpenVPN to work in Windows Azure (this will
            // also probably impact other cloud environments).
            //
            // Azure implements VNETs as layer 3 overlays.  This means that the host network interfaces
            // are not actually on an ethernet segment and the VPN default gateway is actually handling
            // all of the ARP packets, routing between the VNET subnets, load balancers, and the Internet.
            // This is problematic for OpenVPN traffic because the VPN client IP address space is not
            // part of the VNET which means the VNET gateway is not able to route packets from cluster
            // hosts back to the manager's OpenVPN client addresses by default.
            //
            // The solution is to configure the managers with secondary NIC cards in a different subnet
            // and provision special Azure user-defined routes that direct VPN return packets to the
            // correct manager.
            //
            // I figured this part out the second day.  The problem was though that it simply didn't work.
            // From an external VPN client, I would try to ping a worker node through OpenVPN running on
            // a manager.  I'd see the ping traffic:
            //
            //      1. manager/tun0: request
            //      2. manager/eth1: request
            //      3. worker/eth0: request
            //      4. worker/eth0: reply
            //      5. manager/eth0: reply
            //      6: NOTHING! EXPECTED: manager/tun0: reply
            //
            // So the problem was that I could see the ICMP ping request hit the various interfaces 
            // on the manager and be received by the worker.  I'd then see the worker send the reply,
            // and be routed via the user-defined Azure rult back to the manager.  The problem was
            // that the packet was simply dropped there.  It never made it back to tun0 so OpenVPN
            // could forward it back to the client.
            //
            // After days and days of trying to learn about Linux routing, iptables and policy rules,
            // I finally ran across this posting for the second time:
            //
            //      https://unix.stackexchange.com/questions/21093/output-traffic-on-different-interfaces-based-on-destination-port
            //
            // This was the key.  I ran across this a few days ago and didn't read it closely enough.
            // It made more sense after learning more about this stuff.
            //
            // Linux has a built-in IP address spoofing filter enabled by default.  This filter has the
            // kernel discard any packets whose source address doesn't match the IP address/route implied
            // by the remote interface that transmitted the packet.  This is exactly what's happening
            // when Azure forwards the VPN return packets via the user-defined route.  I'd see return
            // packets hit eth0 on the manager, be processed by the low-level RAW and MANGLE iptables
            // and then they'd disappear.
            //
            // The solution is simply to disable the spoofing filter.  I'm going to go ahead and do this
            // for all interfaces which should be fine for clusters hosted in cloud environments, because the
            // VNET/Load Balancer/Security Groups will be used to lock things down.  Local clusters will
            // need to be manually placed behind a suitable router/firewall as well.
            //
            // For robustness, I'm going to deploy this as a service daemon that polls the filter state
            // for each interface every 5 seconds, and disables any enabled filters.  This will ensure
            // that the filters will always be disabled, even as interfaces are bought up and down.

            var disableSpoofUnit =
@"[Unit]
Description=Disable Network Anti-Spoofing Filters
Documentation=
After=
Requires=
Before=

[Service]
Type=simple
ExecStart=/usr/local/bin/disable-spoof-filters.sh

[Install]
WantedBy=multi-user.target
";

            var disableSpoofScript =
@"#!/bin/bash
#------------------------------------------------------------------------------
# This script is a deployed as a service to ensure that the Linux anti-spoofing
# filters are disabled for the network interfaces on manager nodes hosting
# OpenVPN.  This is required to allow VPN return traffic from other nodes to
# routed back to tun0 and ultimately, connected VPN clients.
#
# Note that it appears that we need to disable the filter for all interfaces
# for this to actually work.

while :
do
    flush=false

    for f in /proc/sys/net/ipv4/conf/*/rp_filter
    do
        filter_enabled=$(cat $f)

        if [ ""$filter_enabled"" == ""1"" ] ; then
            echo 0 > $f
            flush=true
        fi
    done

    if [ ""$flush"" == ""true"" ] ; then
      echo 1 > /proc/sys/net/ipv4/route/flush
    fi

    sleep 5
done";

            manager.UploadText("/lib/systemd/system/disable-spoof-filters.service", disableSpoofUnit);
            manager.SudoCommand("chmod 644 /lib/systemd/system/disable-spoof-filters.service");

            manager.UploadText("/usr/local/bin/disable-spoof-filters.sh", disableSpoofScript);
            manager.SudoCommand("chmod 770 /usr/local/bin/disable-spoof-filters.sh");

            manager.SudoCommand("systemctl enable disable-spoof-filters");
            manager.SudoCommand("systemctl restart disable-spoof-filters");
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false);
        }
    }
}
