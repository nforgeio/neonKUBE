//-----------------------------------------------------------------------------
// FILE:	    CommonSteps.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.SSH;

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
        /// <param name="stepDelay">Ignored.</param>
        public static void VerifyOS(NodeSshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            KubeHelper.VerifyNodeOperatingSystem(node);
        }

        /// <summary>
        /// Customizes the OpenSSH configuration on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelayed">Ignored.</param>
        public static void ConfigureOpenSsh(NodeSshProxy<NodeDefinition> node, TimeSpan stepDelayed)
        {
            // Upload the OpenSSH server configuration, restart OpenSSH and
            // then disconnect and wait for the OpenSSH to restart.

            node.UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
            node.SudoCommand("systemctl restart sshd");
        }

        /// <summary>
        /// Configures the global environment variables that describe the configuration 
        /// of the server within the cluster.
        /// </summary>
        /// <param name="node">The server to be updated.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        public static void ConfigureEnvironmentVariables(NodeSshProxy<NodeDefinition> node, ClusterDefinition clusterDefinition)
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
                            if (!line.Contains(KubeNodeFolders.Bin))
                            {
                                sb.AppendLine(line + $":/snap/bin:{KubeNodeFolders.Bin}");
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

            // Add the global cluster related environment variables. 

            sb.AppendLine($"NEON_CLUSTER_PROVISIONER={clusterDefinition.Provisioner}");
            sb.AppendLine($"NEON_CLUSTER={clusterDefinition.Name}");
            sb.AppendLine($"NEON_DATACENTER={clusterDefinition.Datacenter.ToLowerInvariant()}");
            sb.AppendLine($"NEON_ENVIRONMENT={clusterDefinition.Environment.ToString().ToLowerInvariant()}");

            var sbPackageProxies = new StringBuilder();

            if (clusterDefinition.PackageProxy != null)
            {
                foreach (var proxyEndpoint in clusterDefinition.PackageProxy.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    sbPackageProxies.AppendWithSeparator(proxyEndpoint);
                }
            }
            
            sb.AppendLine($"NEON_PACKAGE_PROXY={sbPackageProxies}");

            if (clusterDefinition.Hosting != null)
            {
                sb.AppendLine($"NEON_HOSTING={clusterDefinition.Hosting.Environment.ToMemberString().ToLowerInvariant()}");
            }

            sb.AppendLine($"NEON_NODE_NAME={node.Name}");

            if (node.Metadata != null)
            {
                sb.AppendLine($"NEON_NODE_ROLE={node.Metadata.Role}");
                sb.AppendLine($"NEON_NODE_IP={node.Metadata.Address}");
                sb.AppendLine($"NEON_NODE_HDD={node.Metadata.Labels.StorageHDD.ToString().ToLowerInvariant()}");
            }

            sb.AppendLine($"NEON_BIN_FOLDER={KubeNodeFolders.Bin}");
            sb.AppendLine($"NEON_CONFIG_FOLDER={KubeNodeFolders.Config}");
            sb.AppendLine($"NEON_SETUP_FOLDER={KubeNodeFolders.Setup}");
            sb.AppendLine($"NEON_STATE_FOLDER={KubeNodeFolders.State}");
            sb.AppendLine($"NEON_TMPFS_FOLDER={KubeNodeFolders.Tmpfs}");

            // Kubernetes related variables for masters.

            if (node.Metadata.IsMaster)
            {
                sb.AppendLine($"KUBECONFIG=/etc/kubernetes/admin.conf");
            }

            // Upload the new environment to the server.

            node.UploadText("/etc/environment", sb, tabStop: 4);
        }

        /// <summary>
        /// Configures a node's host public SSH key during node provisioning.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="clusterLogin">The cluster login.</param>
        public static void ConfigureSshKey(NodeSshProxy<NodeDefinition> node, ClusterLogin clusterLogin)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));

            // Configure the SSH credentials on the node.

            node.InvokeIdempotentAction("setup/ssh",
                () =>
                {
                    CommandBundle bundle;

                    // Here's some information explaining what how this works:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    node.Status = "setup: client SSH key";

                    // Enable the public key by appending it to [$HOME/.ssh/authorized_keys],
                    // creating the file if necessary.  Note that we're allowing only a single
                    // authorized key.

                    var addKeyScript =
$@"
chmod go-w ~/
mkdir -p $HOME/.ssh
chmod 700 $HOME/.ssh
touch $HOME/.ssh/authorized_keys
cat ssh-key.ssh2 > $HOME/.ssh/authorized_keys
chmod 600 $HOME/.ssh/authorized_keys
";
                    bundle = new CommandBundle("./addkeys.sh");

                    bundle.AddFile("addkeys.sh", addKeyScript, isExecutable: true);
                    bundle.AddFile("ssh_host_rsa_key", clusterLogin.SshKey.PublicSSH2);

                    // NOTE: I'm explicitly not running the bundle as [sudo] because the OpenSSH
                    //       server is very picky about the permissions on the user's [$HOME]
                    //       and [$HOME/.ssl] folder and contents.  This took me a couple 
                    //       hours to figure out.

                    node.RunCommand(bundle);

                    // These steps are required for both password and public key authentication.

                    // Upload the server key and edit the [sshd] config to disable all host keys 
                    // except for RSA.

                    var configScript =
$@"
# Install public SSH key for the [sysadmin] user.

cp ssh_host_rsa_key.pub /home/{KubeConst.SysAdminUsername}/.ssh/authorized_keys

# Disable all host keys except for RSA.

sed -i 's!^\HostKey /etc/ssh/ssh_host_dsa_key$!#HostKey /etc/ssh/ssh_host_dsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ecdsa_key$!#HostKey /etc/ssh/ssh_host_ecdsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ed25519_key$!#HostKey /etc/ssh/ssh_host_ed25519_key!g' /etc/ssh/sshd_config

# Restart SSHD to pick up the changes.

systemctl restart sshd
";
                    bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    bundle.AddFile("ssh_host_rsa_key.pub", clusterLogin.SshKey.PublicPUB);
                    node.SudoCommand(bundle);
                });

            // Verify that we can login with the new SSH private key and also verify that
            // the password still works.

            node.Status = "ssh: verify private key auth";
            node.Disconnect();
            node.UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUsername, clusterLogin.SshKey.PrivatePEM));
            node.WaitForBoot();

            node.Status = "ssh: verify password auth";
            node.Disconnect();
            node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, clusterLogin.SshPassword));
            node.WaitForBoot();
        }

        /// <summary>
        /// Initializes a near virgin server with the basic capabilities required
        /// for a cluster node.
        /// </summary>
        /// <param name="node">The target cluster node.</param>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="kubeSetupInfo">Kubernetes setup details.</param>
        /// <param name="shutdown">Optionally shuts down the node.</param>
        public static void PrepareNode(NodeSshProxy<NodeDefinition> node, ClusterDefinition clusterDefinition, KubeSetupInfo kubeSetupInfo, HostingManager hostingManager, bool shutdown = false)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(kubeSetupInfo != null, nameof(kubeSetupInfo));

            if (node.FileExists($"{KubeNodeFolders.State}/setup/prepared"))
            {
                return;     // Already prepared
            }

            //-----------------------------------------------------------------
            // Package manager configuration.

            node.Status = "configure: [apt] package manager";

            if (!clusterDefinition.NodeOptions.AllowPackageManagerIPv6)
            {
                // Restrict the [apt] package manager to using IPv4 to communicate
                // with the package mirrors, since IPv6 often doesn't work.
                
                node.UploadText("/etc/apt/apt.conf.d/99-force-ipv4-transport", "Acquire::ForceIPv4 \"true\";");
                node.SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-force-ipv4-transport");
            }

            // Configure [apt] to retry.

            node.UploadText("/etc/apt/apt.conf.d/99-retries", $"APT::Acquire::Retries \"{clusterDefinition.NodeOptions.PackageManagerRetries}\";");
            node.SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-retries");

            //-----------------------------------------------------------------
            // We're going to stop and mask the [snapd.service] if it's running
            // because we don't want it to randomlly update apps on cluster nodes.

            node.Status = "disable: [snapd.service]";

            var disableSnapScript =
@"
# Stop and mask [snapd.service] when it's not already masked.

systemctl status --no-pager snapd.service

if [ $? ]; then
    systemctl stop snapd.service
    systemctl mask snapd.service
fi
";
            node.SudoCommand(CommandBundle.FromScript(disableSnapScript), RunOptions.FaultOnError);

            //-----------------------------------------------------------------
            // Create the standard neonKUBE host folders.

            node.Status = "prepare: neonKUBE host folders";

            node.SudoCommand($"mkdir -p {KubeNodeFolders.Bin}", RunOptions.LogOnErrorOnly);
            node.SudoCommand($"chmod 750 {KubeNodeFolders.Bin}", RunOptions.LogOnErrorOnly);

            node.SudoCommand($"mkdir -p {KubeNodeFolders.Config}", RunOptions.LogOnErrorOnly);
            node.SudoCommand($"chmod 750 {KubeNodeFolders.Config}", RunOptions.LogOnErrorOnly);

            node.SudoCommand($"mkdir -p {KubeNodeFolders.Setup}", RunOptions.LogOnErrorOnly);
            node.SudoCommand($"chmod 750 {KubeNodeFolders.Setup}", RunOptions.LogOnErrorOnly);

            node.SudoCommand($"mkdir -p {KubeNodeFolders.State}", RunOptions.LogOnErrorOnly);
            node.SudoCommand($"chmod 750 {KubeNodeFolders.State}", RunOptions.LogOnErrorOnly);

            node.SudoCommand($"mkdir -p {KubeNodeFolders.State}/setup", RunOptions.LogOnErrorOnly);
            node.SudoCommand($"chmod 750 {KubeNodeFolders.State}/setup", RunOptions.LogOnErrorOnly);

            //-----------------------------------------------------------------
            // Other configuration.

            node.Status = "configure: journald filters";

            var filterScript =
@"
# neonKUBE: 
#
# Filter [rsyslog.service] log events we don't care about.

cat <<EOF > /etc/rsyslog.d/60-filter.conf
if $programname == ""systemd"" and ($msg startswith ""Created slice "" or $msg startswith ""Removed slice "") then stop
EOF

systemctl restart rsyslog.service
";
            node.SudoCommand(CommandBundle.FromScript(filterScript), RunOptions.FaultOnError);

            node.Status = "configure: openssh";

            ConfigureOpenSsh(node, TimeSpan.Zero);

            node.Status = "upload: prepare files";

            node.UploadConfigFiles(clusterDefinition, kubeSetupInfo);
            node.UploadResources(clusterDefinition, kubeSetupInfo);

            node.Status = "configure: environment vars";

            if (clusterDefinition != null)
            {
                ConfigureEnvironmentVariables(node, clusterDefinition);
            }

            node.SudoCommand("safe-apt-get update");

            node.InvokeIdempotentAction("setup/prep-node",
                () =>
                {
                    node.Status = "prepare: node";
                    node.SudoCommand("setup-prep.sh");
                    node.Reboot(wait: true);
                });

            // We need to upload the cluster configuration and initialize drives attached 
            // to the node.  We're going to assume that these are not already initialized.

            node.Status = "setup: disk";

            var diskName  = hostingManager.GetDataDisk(node);
            var partition = char.IsDigit(diskName.Last()) ? $"{diskName}p1" : $"{diskName}1";

            node.SudoCommand("setup-disk.sh", diskName, partition);

            // Clear any DHCP leases to be super sure that cloned node
            // VMs will obtain fresh IP addresses.

            node.Status = "clear: DHCP leases";
            node.SudoCommand("rm -f /var/lib/dhcp/*");

            // Indicate that the node has been fully prepared.

            node.SudoCommand($"touch {KubeNodeFolders.State}/setup/prepared");

            // Shutdown the node if requested.

            if (shutdown)
            {
                node.Status = "shutdown";
                node.SudoCommand("shutdown 0", RunOptions.Defaults | RunOptions.Shutdown);
            }
        }
    }
}
