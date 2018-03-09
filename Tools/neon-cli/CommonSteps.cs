//-----------------------------------------------------------------------------
// FILE:	    CommonSteps.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements common configuration steps.
    /// </summary>
    public static class CommonSteps
    {
        /// <summary>
        /// Verifies that the node has the correct operating system installed.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        public static void VerifyOS(SshProxy<NodeDefinition> node)
        {
            node.Status = "verify: operating system";

            var response = node.SudoCommand("lsb_release -a");

            switch (Program.OSProperties.TargetOS)
            {
                case TargetOS.Ubuntu_16_04:

                    if (!response.OutputText.Contains("Ubuntu 16.04"))
                    {
                        node.Fault("Expected [Ubuntu 16.04].");
                    }
                    break;

                default:

                    throw new NotImplementedException($"Support for [{nameof(TargetOS)}.{Program.OSProperties.TargetOS}] is not implemented.");
            }
        }

        /// <summary>
        /// Customizes the OpenSSH configuration on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void ConfigureOpenSSH(SshProxy<NodeDefinition> node)
        {
            // Upload the OpenSSH server configuration, restart OpenSSH and
            // then disconnect and wait for the OpenSSH to restart.

            var openSshConfig =
@"# Package generated configuration file
# See the sshd_config(5) manpage for details

# What ports, IPs and protocols we listen for
Port 22
# Use these options to restrict which interfaces/protocols sshd will bind to
#ListenAddress ::
#ListenAddress 0.0.0.0
Protocol 2
# HostKeys for protocol version 2
HostKey /etc/ssh/ssh_host_rsa_key
HostKey /etc/ssh/ssh_host_dsa_key
HostKey /etc/ssh/ssh_host_ecdsa_key
HostKey /etc/ssh/ssh_host_ed25519_key
#Privilege Separation is turned on for security
UsePrivilegeSeparation yes

# Lifetime and size of ephemeral version 1 server key
KeyRegenerationInterval 3600
ServerKeyBits 1024

# Logging
SyslogFacility AUTH
LogLevel INFO

# Authentication:
LoginGraceTime 120
PermitRootLogin prohibit-password
StrictModes yes

RSAAuthentication yes
PubkeyAuthentication yes
#AuthorizedKeysFile	%h/.ssh/authorized_keys

# Don't read the user's ~/.rhosts and ~/.shosts files
IgnoreRhosts yes
# For this to work you will also need host keys in /etc/ssh_known_hosts
RhostsRSAAuthentication no
# similar for protocol version 2
HostbasedAuthentication no
# Uncomment if you don't trust ~/.ssh/known_hosts for RhostsRSAAuthentication
#IgnoreUserKnownHosts yes

# To enable empty passwords, change to yes (NOT RECOMMENDED)
PermitEmptyPasswords no

# Change to yes to enable challenge-response passwords (beware issues with
# some PAM modules and threads)
ChallengeResponseAuthentication no

# Change to no to disable tunnelled clear text passwords
#PasswordAuthentication yes

# Kerberos options
#KerberosAuthentication no
#KerberosGetAFSToken no
#KerberosOrLocalPasswd yes
#KerberosTicketCleanup yes

# GSSAPI options
#GSSAPIAuthentication no
#GSSAPICleanupCredentials yes

AllowTcpForwarding no
X11Forwarding no
X11DisplayOffset 10
PrintMotd no
PrintLastLog yes
TCPKeepAlive yes
#UseLogin no

#MaxStartups 10:30:60
#Banner /etc/issue.net

# Allow client to pass locale environment variables
AcceptEnv LANG LC_*

Subsystem sftp /usr/lib/openssh/sftp-server

# Set this to 'yes' to enable PAM authentication, account processing,
# and session processing. If this is enabled, PAM authentication will
# be allowed through the ChallengeResponseAuthentication and
# PasswordAuthentication.  Depending on your PAM configuration,
# PAM authentication via ChallengeResponseAuthentication may bypass
# the setting of ""PermitRootLogin without-password"".
# If you just want the PAM account and session checks to run without
# PAM authentication, then enable this but set PasswordAuthentication
# and ChallengeResponseAuthentication to 'no'.
UsePAM yes

# Allow connections to be idle for up to an 10 minutes (600 seconds)
# before terminating them.  This configuration pings the client every
# 30 seconds for up to 20 times without a response:
#
#   20*30 = 600 seconds

ClientAliveInterval 30
ClientAliveCountMax 20
TCPKeepAlive yes
";
            node.UploadText("/etc/ssh/ssh_config", openSshConfig);
            node.SudoCommand("systemctl restart sshd");
        }

        /// <summary>
        /// Waits for the Linux package manager to report being ready.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <remarks>
        /// The package manager is often busy after a reboot, installing updates.
        /// This method polls the package manager status until it reports ready.
        /// </remarks>
        public static void WaitForPackageManager(SshProxy<NodeDefinition> node)
        {
            // $todo(jeff.lill): Remove this method in the future.
            //
            // I finally figured out that I can disable the [apt-daily] service
            // to avoid having to perform this lengthly (and fragile) check.
            //
            // I'm going to make this a NOP and retain the calls to this method
            // for the time being, in case I need to revert this for some reason.
#if TODO
            node.Status = "package manager check";

            // Pause to give Linux a chance to boot and actually start any
            // pending package manager operations.

            Thread.Sleep(TimeSpan.FromSeconds(30));

            // Wait for the package manager to report ready.

            while (true)
            {
                if (node.SudoCommand("apt-get check", RunOptions.LogOnErrorOnly).ExitCode == 0)
                {
                    break;
                }

                node.Status = "package manager busy";
            }

            node.Status = "package manager ready";
#endif
        }

        /// <summary>
        /// Configures the global environment variables that describe the configuration 
        /// of the server within the cluster.
        /// </summary>
        /// <param name="node">The server to be updated.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        public static void ConfigureEnvironmentVariables(SshProxy<NodeDefinition> node, ClusterDefinition clusterDefinition)
        {
            node.Status = "environment variables";

            // We're going to append the new variables to the existing Linux [/etc/environment] file.

            var sb = new StringBuilder();

            // Append all of the existing environment variables except for those
            // whose names start with "NEON_" to make the operation idempotent.
            //
            // Note that we're going to special case PATH to add any Neon
            // related directories.

            using (var currentEnvironmentStream = new MemoryStream())
            {
                node.Download("/etc/environment", currentEnvironmentStream);

                currentEnvironmentStream.Position = 0;

                using (var reader = new StreamReader(currentEnvironmentStream))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (line.StartsWith("PATH="))
                        {
                            if (!line.Contains(NodeHostFolders.Tools))
                            {
                                sb.AppendLine(line + $":{NodeHostFolders.Tools}");
                            }
                            else
                            {
                                sb.AppendLine(line);
                            }
                        }
                        else if (!line.StartsWith("NEON_"))
                        {
                            sb.AppendLine(line);
                        }
                    }
                }
            }

            // Add the global Neon host and cluster related environment variables. 

            sb.AppendLine($"NEON_CLUSTER_PROVISIONER={clusterDefinition.Provisioner}");
            sb.AppendLine($"NEON_CLUSTER={clusterDefinition.Name}");
            sb.AppendLine($"NEON_DATACENTER={clusterDefinition.Datacenter.ToLowerInvariant()}");
            sb.AppendLine($"NEON_ENVIRONMENT={clusterDefinition.Environment.ToString().ToLowerInvariant()}");

            if (clusterDefinition.Hosting != null)
            {
                sb.AppendLine($"NEON_HOSTING={clusterDefinition.Hosting.Environment.ToMemberString().ToLowerInvariant()}");
            }

            sb.AppendLine($"NEON_NODE_NAME={node.Name}");
            sb.AppendLine($"NEON_NODE_CFS={clusterDefinition.Ceph.Enabled.ToString().ToLowerInvariant()}");

            if (node.Metadata != null)
            {
                sb.AppendLine($"NEON_NODE_ROLE={node.Metadata.Role}");
                sb.AppendLine($"NEON_NODE_IP={node.Metadata.PrivateAddress}");
                sb.AppendLine($"NEON_NODE_SSD={node.Metadata.Labels.StorageSSD.ToString().ToLowerInvariant()}");
                sb.AppendLine($"NEON_NODE_SWAP={node.Metadata.Labels.ComputeSwap.ToString().ToLowerInvariant()}");
            }

            sb.AppendLine($"NEON_APT_PROXY={NeonClusterHelper.GetPackageProxyReferences(clusterDefinition)}");

            // Append Consul and Vault addresses.

            // All nodes will be configured such that host processes using the HashiCorp Consul 
            // CLI will access the Consul cluster via local Consul instance.  This will be a 
            // server for manager nodes and a proxy for workers.

            sb.AppendLine($"CONSUL_HTTP_ADDR=" + $"{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");
            sb.AppendLine($"CONSUL_HTTP_FULLADDR=" + $"http://{NeonHosts.Consul}:{clusterDefinition.Consul.Port}");

            // All nodes will be configured such that host processes using the HashiCorp Vault 
            // CLI will access the Vault cluster via the [neon-proxy-vault] proxy service
            // by default.

            sb.AppendLine($"VAULT_ADDR={clusterDefinition.Vault.Uri}");

            if (node.Metadata != null)
            {
                if (node.Metadata.IsManager)
                {
                    // Manager hosts may use the [VAULT_DIRECT_ADDR] environment variable to 
                    // access Vault without going through the [neon-proxy-vault] proxy.  This
                    // points to the Vault instance running locally.
                    //
                    // This is useful when configuring Vault.

                    sb.AppendLine($"VAULT_DIRECT_ADDR={clusterDefinition.Vault.GetDirectUri(node.Name)}");
                }
                else
                {
                    sb.AppendLine($"VAULT_DIRECT_ADDR=");
                }
            }

            // Upload the new environment to the server.

            node.UploadText("/etc/environment", sb.ToString(), tabStop: 4);
        }

        /// <summary>
        /// Initializes a near virgin server with the basic capabilities required
        /// for a neonCLUSTER host node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="shutdown">Optionally shuts down the node.</param>
        /// <returns>
        /// <c>true</c> if the method waited for the package manager to become
        /// ready before returning.
        /// </returns>
        public static bool PrepareNode(SshProxy<NodeDefinition> node, ClusterDefinition clusterDefinition, bool shutdown = false)
        {
            var waitedForPackageManager = false;

            if (node.FileExists($"{NodeHostFolders.State}/finished-prepared"))
            {
                return waitedForPackageManager; // Already prepared
            }

            if (!clusterDefinition.HostNode.AllowPackageManagerIPv6)
            {
                // Restrict the [apt] package manager to using IPv4 to communicate
                // with the package mirrors, since IPv6 often doesn't work.

                node.UploadText("/etc/apt/apt.conf.d/1000-force-ipv4-transport", "Acquire::ForceIPv4 \"true\";");
                node.SudoCommand("chmod 644 /etc/apt/apt.conf.d/1000-force-ipv4-transport");
            }

            ConfigureOpenSSH(node);

            node.InitializeNeonFolders();
            node.UploadConfigFiles(clusterDefinition);
            node.UploadTools(clusterDefinition);

            WaitForPackageManager(node);

            if (clusterDefinition != null)
            {
                ConfigureEnvironmentVariables(node, clusterDefinition);
            }

            node.SudoCommand("apt-get update");

            node.InvokeIdempotentAction("setup-prep-node",
                () =>
                {
                    node.Status = "run: setup-prep-node.sh";
                    node.SudoCommand("setup-prep-node.sh");

                    // Wait for the server a chance to perform any post-update activities
                    // and then reboot and wait for any post-boot package manager activities.

                    WaitForPackageManager(node);
                    node.Reboot(wait: true);
                    WaitForPackageManager(node);

                    waitedForPackageManager = true;
                });

            // We need to upload the cluster configuration and initialize drives attached 
            // to the node.  We're going to assume that these are not already initialized.

            // $todo(jeff.lill): 
            //
            // We may need an option that allows an operator to pre-build a hardware
            // based drive array or something.  I'm going to defer this to later and
            // concentrate on commodity hardware and cloud deployments for now. 

            CommonSteps.ConfigureEnvironmentVariables(node, clusterDefinition);

            node.Status = "run: setup-disk.sh";
            node.SudoCommand("setup-disk.sh");

            // Clear any DHCP leases to be super sure that cloned node
            // VMs will obtain fresh IP addresses.

            node.Status = "clear DHCP leases";
            node.SudoCommand("rm -f /var/lib/dhcp/*");

            // Indicate that the node has been fully prepared.

            node.SudoCommand($"touch {NodeHostFolders.State}/finished-prepared");

            // Shutdown the node if requested.

            if (shutdown)
            {
                node.Status = "shutdown";
                node.SudoCommand("shutdown 0", RunOptions.Defaults | RunOptions.Shutdown);
            }

            return waitedForPackageManager;
        }
    }
}
