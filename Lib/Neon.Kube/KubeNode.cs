//-----------------------------------------------------------------------------
// FILE:	    KubeNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Implements lower-level node preparation methods.
    /// </summary>
    public static class KubeNode
    {
        /// <summary>
        /// Installs hypervisor guest integration services.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void InstallGuestIntegrationServices(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "install: guest integration services";

            var guestServicesScript =
@"#!/bin/bash
cat <<EOF >> /etc/initramfs-tools/modules
hv_vmbus
hv_storvsc
hv_blkvsc
hv_netvsc
EOF

apt-get install -yq --allow-downgrades linux-virtual linux-cloud-tools-virtual linux-tools-virtual
update-initramfs -u
";

            node.SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Disables DHCP.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void DisableDhcp(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "disable: DHCP";

            var initNetPlanScript =
$@"
rm /etc/netplan/*

cat <<EOF > /etc/netplan/no-dhcp.yaml
# This file is used to disable the network when a new VM created from
# a template is booted.  The [neon-init] service handles network
# provisioning in conjunction with the cluster prepare step.
#
# Cluster prepare inserts a virtual DVD disc with a script that
# handles the network configuration which [neon-init] will
# execute.

network:
  version: 2
  renderer: networkd
  ethernets:
    eth0:
      dhcp4: no
EOF
";
            node.SudoCommand(CommandBundle.FromScript(initNetPlanScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Disables <b>cloud-init</b>.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void DisableCloudInit(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "disable: cloud-init";

            var disableCloudInitScript =
$@"
touch /etc/cloud/cloud-init.disabled
";
            node.SudoCommand(CommandBundle.FromScript(disableCloudInitScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Cleans a node by removing unnecessary package manager metadata, cached DHCP information, etc.
        /// and then fills unreferenced file system blocks and nodes with zeros so the disk image will
        /// compress better.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void Clean(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "clean: VM";

            var cleanScript =
@"#!/bin/bash
cloud-init clean
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
            node.SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Customizes the OpenSSH configuration on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void ConfigureOpenSsh(NodeSshProxy<NodeDefinition> node)
        {
            // Upload the OpenSSH server configuration and restart OpenSSH.

            node.Status = "configure: OpenSSH";

            node.UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
            node.SudoCommand("systemctl restart sshd");
        }

        /// <summary>
        /// Removes unnecessary packages.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void CleanPackages(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "Remove: Unnecessary packages";

            // $todo(jefflill): Implement this.

#if TODO
            // Remove unnecessary packages.

            var removePackagesScript =
@"
apt-get remove -y \
    locales \
    snapd \
    iso-codes \
    git \
    vim-runtime vim-tiny \
    manpages man-db \
    cloud-init \
    python3-twisted \
    perl perl-base perl-modules-5.30 libperl5.30 \
    linux-modules-extra-5.4.0.60-generic \
    unused hypervisor integration services?

apt-get autoremove -y
";
            node.SudoCommand(CommandBundle.FromScript(removePackagesScript);
#endif
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

cp ssh_host_rsa_key.pub /home/{KubeConst.SysAdminUser}/.ssh/authorized_keys

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
            node.UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, clusterLogin.SshKey.PrivatePEM));
            node.WaitForBoot();

            node.Status = "ssh: verify password auth";
            node.Disconnect();
            node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword));
            node.WaitForBoot();
        }

        /// <summary>
        /// Configures the APY package manager.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="packageManagerRetries">Optionally specifies the packager manager retries (defaults to <b>5</b>).</param>
        /// <param name="allowPackageManagerIPv6">Optionally prevent the package manager from using IPv6 (defaults to <c>false</c>.</param>
        public static void ConfigureApt(NodeSshProxy<NodeDefinition> node, int packageManagerRetries = 5, bool allowPackageManagerIPv6 = false)
        {
            node.Status = "configure: [apt] package manager";

            if (!allowPackageManagerIPv6)
            {
                // Restrict the [apt] package manager to using IPv4 to communicate
                // with the package mirrors, since IPv6 doesn't work sometimes.

                node.UploadText("/etc/apt/apt.conf.d/99-force-ipv4-transport", "Acquire::ForceIPv4 \"true\";");
                node.SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-force-ipv4-transport", RunOptions.FaultOnError);
            }

            // Configure [apt] to retry.

            node.UploadText("/etc/apt/apt.conf.d/99-retries", $"APT::Acquire::Retries \"{packageManagerRetries}\";");
            node.SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-retries", RunOptions.FaultOnError);

            // We're going to disable apt updating services so we can control when this happens.

            var disableAptServices =
@"#------------------------------------------------------------------------------
# Disable the [apt-timer] and [apt-daily] services.  We're doing this 
# for two reasons:
#
#   1. These services interfere with with [apt-get] usage during
#      cluster setup and is also likely to interfere with end-user
#      configuration activities as well.
#
#   2. Automatic updates for production and even test clusters is
#      just not a great idea.  You just don't want a random update
#      applied in the middle of the night which might cause trouble.
#
#      We're going to implement our own cluster updating machanism
#      that will be smart enough to update the nodes such that the
#      impact on cluster workloads will be limited.

systemctl stop apt-daily.timer
systemctl mask apt-daily.timer

systemctl stop apt-daily.service
systemctl mask apt-daily.service

# It may be possible for the auto updater to already be running so we'll
# wait here for it to release any lock files it holds.

while fuser /var/{{lib /{{dpkg,apt/lists}},cache/apt/archives}}/lock; do
    sleep 1
done";
            node.SudoCommand(CommandBundle.FromScript(disableAptServices), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Disables the SNAP package manasger.
        /// </summary>
        /// <param name="node">The target node.</param>
        public static void DisableSnap(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "disable: [snapd.service]";

            var disableSnapScript =
@"# Stop and mask [snapd.service] if it's not already masked.

systemctl status --no-pager snapd.service

if [ $? ]; then
    systemctl stop snapd.service
    systemctl mask snapd.service
fi
";
            node.SudoCommand(CommandBundle.FromScript(disableSnapScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Installs the <b>neon-init</b> service which is a cloud-init like service we
        /// use to configure the network and credentials for VMs hosted in non-cloud
        /// hypervisors.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <remarks>
        /// <para>
        /// Install and configure the [neon-init] service.  This is a simple script
        /// that is configured to run as a oneshot systemd service before networking is
        /// started.  This is currently used to configure the node's static IP address
        /// configuration on first boot, so we don't need to rely on DHCP (which may not
        /// be available in some environments).
        /// </para>
        /// <para>
        /// [neon-init] is intended to run the first time a node is booted after
        /// being created from a template.  It checks to see if a special ISO with a
        /// configuration script named [neon-init.sh] is inserted into the VMs DVD
        /// drive and when present, the script will be executed and the [/etc/neon-init]
        /// file will be created to indicate that the service no longer needs to do this for
        /// subsequent reboots.
        /// </para>
        /// <note>
        /// The script won't create the [/etc/neon-init] when the script ISO doesn't exist 
        /// for debugging purposes.
        /// </note>
        /// </remarks>
        public static void InstallNeonInit(NodeSshProxy<NodeDefinition> node)
        {
            node.Status = "install: [neon-init]";

            var neonNodePrepScript =
$@"# Ensure that the neon binary folder exists.

mkdir -p {KubeNodeFolders.Bin}

# Create the systemd unit file.

cat <<EOF > /etc/systemd/system/neon-init.service

[Unit]
Description=neonKUBE one-time node preparation service 
After=systemd-networkd.service

[Service]
Type=oneshot
ExecStart={KubeNodeFolders.Bin}/neon-init.sh
RemainAfterExit=false
StandardOutput=journal+console

[Install]
WantedBy=multi-user.target
EOF

# Create the service script.

cat <<EOF > {KubeNodeFolders.Bin}/neon-init.sh
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:	        neon-init.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the ""License"");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an ""AS IS"" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# This script is run early during node boot before the netork is configured
# as a poor man's way for neonKUBE cluster setup to configure the network
# without requiring DHCP.  Here's how this works:
#
#       1. neonKUBE cluster setup creates a node VM from a template.
#
#       2. Setup creates a temporary ISO (DVD) image with a script named 
#          [neon-init.sh] on it and uploads this to the Hyper-V
#          or XenServer host machine.
#
#       3. Setup inserts the VFD into the VM's DVD drive and starts the VM.
#
#       4. The VM boots, eventually running this script (via the
#          [neon-init] service).
#
#       5. This script checks whether a DVD is present, mounts
#          it and checks it for the [neon-init.sh] script.
#
#       6. If the DVD and script file are present, this service will
#          execute the script via Bash, peforming any required custom setup.
#          Then this script creates the [/etc/neon-init] file which 
#          prevents the service from doing anything during subsequent node 
#          reboots.
#
#       7. The service just exits if the DVD and/or script file are 
#          not present.  This shouldn't happen in production but is useful
#          for script debugging.

# Run the prep script only once.

if [ -f /etc/neon-init ] ; then
    echo ""INFO: Machine is already prepared.""
    exit 0
fi

# Check for the DVD and prep script.

mkdir -p /media/neon-init

if [ ! $? ] ; then
    echo ""ERROR: Cannot create DVD mount point.""
    rm -rf /media/neon-init
    exit 1
fi

mount /dev/dvd /media/neon-init

if [ ! $? ] ; then
    echo ""WARNING: No DVD is present.""
    rm -rf /media/neon-init
    exit 0
fi

if [ ! -f /media/neon-init/neon-init.sh ] ; then
    echo ""WARNING: No [neon-init.sh] script is present on the DVD.""
    rm -rf /media/neon-init
    exit 0
fi

# The script file is present so execute it.  Note that we're
# passing the path where the DVD is mounted as a parameter.

echo ""INFO: Running [neon-init.sh]""
bash /media/neon-init/neon-init.sh /media/neon-init

# Unmount the DVD and cleanup.

echo ""INFO: Cleanup""
umount /media/neon-init
rm -rf /media/neon-init

# Disable [neon-init] the next time it is started.

touch /etc/neon-init
EOF

chmod 744 {KubeNodeFolders.Bin}/neon-init.sh

# Configure [neon-init] to start at boot.

systemctl enable neon-init
systemctl daemon-reload
";
            node.SudoCommand(CommandBundle.FromScript(neonNodePrepScript), RunOptions.FaultOnError);
        }
    }
}
