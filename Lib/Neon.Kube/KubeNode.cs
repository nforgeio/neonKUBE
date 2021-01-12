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
        /// Ensures that the node operating system and version is supported for a neonKUBE
        /// cluster.  This faults the nodeproxy on faliure.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        /// <returns><c>true</c> if the operation system is supported.</returns>
        public static bool VerifyNodeOS(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            KubeHelper.LogStatus(logWriter, "Check", "OS");
            node.Status = "check: OS";

            // $todo(jefflill): We're currently hardcoded to Ubuntu 20.04.x

            if (!node.OsName.Equals("Ubuntu", StringComparison.InvariantCultureIgnoreCase) || node.OsVersion < Version.Parse("20.04"))
            {
                node.Fault("Expected: Ubuntu 20.04+");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs low-level initialization of a cluster node.  This is applied one time to
        /// Hyper-V and XenServer/XCP-ng node templates when they are created and at cluster
        /// creation time for cloud and bare metal based clusters.  The node must already
        /// be booted and running.
        /// </summary>
        /// <param name="node">The node's SSH proxy.</param>
        /// <param name="sshPassword">The current <b>sysadmin</b> password.</param>
        /// <param name="updateDistribution">Optionally upgrade the node's Linux distribution.  This defaults to <c>false</c>.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void Initialize(NodeSshProxy<NodeDefinition> node, string sshPassword, bool updateDistribution = false, Action<string> logWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sshPassword), nameof(sshPassword));

            // $hack(jefflill):
            //
            // This method is going to be called for two different scenarios that will each
            // call for different logging mechanisms.
            //
            //      1. For the [neon prepare node-template] command, we're simply going 
            //         to write status to the console as lines via the [logWriter].
            //
            //      2. For node preparation for cloud and bare metal clusters, we're
            //         going to set the node status and use the standard setup progress
            //         mechanism to display the status.
            //
            // [logWriter] will be NULL for the second scenario so we'll call the log helper
            // method above which won't do anything.
            //
            // For scenario #1, there is no setup display mechanism, so updating node status
            // won't actually display anything, so we'll just set the status as well without
            // harming anything.

            // Wait for boot/connect.

            KubeHelper.LogStatus(logWriter, "Login", $"[{KubeConst.SysAdminUser}]");
            node.Status = $"login: [{KubeConst.SysAdminUser}]";

            node.WaitForBoot();

            // Disable and mask the auto update services to avoid conflicts with
            // our package operations.  We're going to implement our own cluster
            // updating mechanism.

            KubeHelper.LogStatus(logWriter, "Disable", $"auto updates");
            node.Status = "disable: auto updates";

            node.SudoCommand("systemctl stop snapd.service", RunOptions.None);
            node.SudoCommand("systemctl mask snapd.service", RunOptions.None);

            node.SudoCommand("systemctl stop apt-daily.timer", RunOptions.None);
            node.SudoCommand("systemctl mask apt-daily.timer", RunOptions.None);

            node.SudoCommand("systemctl stop apt-daily.service", RunOptions.None);
            node.SudoCommand("systemctl mask apt-daily.service", RunOptions.None);

            // Wait for the apt-get lock to be released if somebody is holding it.

            KubeHelper.LogStatus(logWriter, "Wait", "for pending updates");
            node.Status = "wait: for pending updates";

            while (node.SudoCommand("fuser /var/{lib/{dpkg,apt/lists},cache/apt/archives}/lock", RunOptions.None).ExitCode == 0)
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            // Disable sudo password prompts and reconnect.

            KubeHelper.LogStatus(logWriter, "Disable", "[sudo] password");
            node.Status = "disable: sudo password";
            node.DisableSudoPrompt(sshPassword);

            KubeHelper.LogStatus(logWriter, "Login", $"[{KubeConst.SysAdminUser}]");
            node.Status = "reconnecting...";
            node.WaitForBoot();

            // Install required packages and ugrade the distribution if requested.

            KubeHelper.LogStatus(logWriter, "Install", "packages");
            node.Status = "install: packages";
            node.SudoCommand("apt-get update", RunOptions.FaultOnError);
            node.SudoCommand("apt-get install -yq --allow-downgrades zip secure-delete", RunOptions.FaultOnError);

            if (updateDistribution)
            {
                KubeHelper.LogStatus(logWriter, "Upgrade", "linux");
                node.Status = "upgrade linux";
                node.SudoCommand("apt-get dist-upgrade -yq");
            }

            // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img] line.

            KubeHelper.LogStatus(logWriter, "Disable", "swap");
            node.Status = "disable: swap";

            var sbFsTab = new StringBuilder();

            using (var reader = new StringReader(node.DownloadText("/etc/fstab")))
            {
                foreach (var line in reader.Lines())
                {
                    if (!line.Contains("/swap.img"))
                    {
                        sbFsTab.AppendLine(line);
                    }
                }
            }

            node.UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");

            // We need to relocate the [sysadmin] UID/GID to 1234 so we
            // can create the [container] user and group at 1000.  We'll
            // need to create a temporary user with root permissions to
            // delete and then recreate the [sysadmin] account.

            KubeHelper.LogStatus(logWriter, "Create", "[temp] user");
            node.Status = "create: [temp] user";

            var tempUserScript =
$@"#!/bin/bash

# Create the [temp] user.

useradd --uid 5000 --create-home --groups root temp
echo 'temp:{sshPassword}' | chpasswd
chown temp:temp /home/temp

# Add [temp] to the same groups that [sysadmin] belongs to
# other than the [sysadmin] group.

adduser temp adm
adduser temp cdrom
adduser temp sudo
adduser temp dip
adduser temp plugdev
adduser temp lxd
";
            node.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);

            // Reconnect with the [temp] account so we can relocate the [sysadmin]
            // user and its group ID to ID=1234.

            KubeHelper.LogStatus(logWriter, "Login", "[temp]");
            node.Status = "login: [temp]";

            node.UpdateCredentials(SshCredentials.FromUserPassword("temp", sshPassword));
            node.Connect();

            // Beginning with Ubuntu 20.04 we're seeing [systemd/(sd-pam)] processes 
            // hanging around for a while for the [sysadmin] user which prevents us 
            // from deleting the [temp] user below.  We're going to handle this by
            // killing any [temp] user processes first.

            KubeHelper.LogStatus(logWriter, "Kill", "[sysadmin] user processes");
            node.Status = "kill: [sysadmin] processes";
            node.SudoCommand("pkill -u sysadmin --signal 9");

            // Relocate the [sysadmin] user to from [uid=1000:gid=1000} to [1234:1234]:

            var sysadminUserScript =
$@"#!/bin/bash

# Update all file references from the old to new [sysadmin]
# user and group IDs:

find / -group 1000 -exec chgrp -h {KubeConst.SysAdminGroup} {{}} \;
find / -user 1000 -exec chown -h {KubeConst.SysAdminUser} {{}} \;

# Relocate the [sysadmin] UID and GID:

groupmod --gid {KubeConst.SysAdminGID} {KubeConst.SysAdminGroup}
usermod --uid {KubeConst.SysAdminUID} --gid {KubeConst.SysAdminGID} --groups root,sysadmin,sudo {KubeConst.SysAdminUser}
";
            KubeHelper.LogStatus(logWriter, "Relocate", "[sysadmin] user/group IDs");
            node.Status = "relocate: [sysadmin] user/group IDs";
            node.SudoCommand(CommandBundle.FromScript(sysadminUserScript), RunOptions.FaultOnError);

            KubeHelper.LogStatus(logWriter, "Logout");
            node.Status = "logout";

            // We need to reconnect again with [sysadmin] so we can remove
            // the [temp] user, create the [container] user and then
            // wrap things up.

            node.SudoCommand(CommandBundle.FromScript(tempUserScript), RunOptions.FaultOnError);
            KubeHelper.LogStatus(logWriter, "Login", $"[{KubeConst.SysAdminUser}]");
            node.Status = $"login: [{KubeConst.SysAdminUser}]";

            node.UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, sshPassword));
            node.Connect();

            // Beginning with Ubuntu 20.04 we're seeing [systemd/(sd-pam)] processes 
            // hanging around for a while for the [temp] user which prevents us 
            // from deleting the [temp] user below.  We're going to handle this by
            // killing any [temp] user processes first.

            KubeHelper.LogStatus(logWriter, "Kill", "[temp] user processes");
            node.Status = "kill: [temp] user processes";
            node.SudoCommand("pkill -u temp");

            // Remove the [temp] user.

            KubeHelper.LogStatus(logWriter, "Remove", "[temp] user");
            node.Status = "remove: [temp] user";
            node.SudoCommand($"rm -rf /home/temp", RunOptions.FaultOnError);

            // Ensure that the owner and group for files in the [sysadmin]
            // home folder are correct.

            KubeHelper.LogStatus(logWriter, "Set", "[sysadmin] home folder owner");
            node.Status = "set: [sysadmin] home folder owner";
            node.SudoCommand($"chown -R {KubeConst.SysAdminUser}:{KubeConst.SysAdminGroup} .*", RunOptions.FaultOnError);

            // Create the [container] user with no home directory.  This
            // means that the [container] user will have no chance of
            // logging into the machine.

            KubeHelper.LogStatus(logWriter, $"Create", $"[{KubeConst.ContainerUsername}] user");
            node.Status = $"create: [{KubeConst.ContainerUsername}] user";
            node.SudoCommand($"useradd --uid {KubeConst.ContainerUID} --no-create-home {KubeConst.ContainerUsername}", RunOptions.FaultOnError);
        }

        /// <summary>
        /// Installs hypervisor guest integration services.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void InstallGuestIntegrationServices(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Install", "Guest integration services");
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
        /// <param name="logWriter">Optional log writer action.</param>
        public static void DisableDhcp(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Disable", "DHCP");
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
        /// <param name="logWriter">Optional log writer action.</param>
        public static void DisableCloudInit(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Disable", "cloud-init");
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
        /// <param name="logWriter">Optional log writer action.</param>
        public static void Clean(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Clean", "VM");
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
        /// <param name="logWriter">Optional log writer action.</param>
        public static void ConfigureOpenSsh(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            // Upload the OpenSSH server configuration and restart OpenSSH.

            KubeHelper.LogStatus(logWriter, "Configure", "OpenSSH");
            node.Status = "configure: OpenSSH";

            node.UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
            node.SudoCommand("systemctl restart sshd");
        }

        /// <summary>
        /// Removes unnecessary packages.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void CleanPackages(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Remove", "Unnecessary packages");
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
        /// Configures the APY package manager.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="packageManagerRetries">Optionally specifies the packager manager retries (defaults to <b>5</b>).</param>
        /// <param name="allowPackageManagerIPv6">Optionally prevent the package manager from using IPv6 (defaults to <c>false</c>.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void ConfigureApt(NodeSshProxy<NodeDefinition> node, int packageManagerRetries = 5, bool allowPackageManagerIPv6 = false, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Configure", "[apt] package manager");
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
        /// <param name="logWriter">Optional log writer action.</param>
        public static void DisableSnap(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Disable", "[snapd.service]");
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
        /// <param name="logWriter">Optional log writer action.</param>
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
        public static void InstallNeonInit(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Install", "neon-init.service");
            node.Status = "install: neon-init.service";

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

        /// <summary>
        /// Create the node folders required by neoneKUBE.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void CreateKubeFolders(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Create", "Cluster folders");
            node.Status = "create: cluster folders";

            var folderScript =
$@"
mkdir -p {KubeNodeFolders.Bin}
chmod 750 {KubeNodeFolders.Bin}

mkdir -p {KubeNodeFolders.Config}
chmod 750 {KubeNodeFolders.Config}

mkdir -p {KubeNodeFolders.Setup}
chmod 750 {KubeNodeFolders.Setup}

mkdir -p {KubeNodeFolders.Helm}
chmod 750 {KubeNodeFolders.Helm}

mkdir -p {KubeNodeFolders.State}
chmod 750 {KubeNodeFolders.State}

mkdir -p {KubeNodeFolders.State}/setup
chmod 750 {KubeNodeFolders.State}/setup
";
            node.SudoCommand(CommandBundle.FromScript(folderScript), RunOptions.LogOnErrorOnly);
        }

        /// <summary>
        /// <para>
        /// Installs the tool scripts, making them executable.
        /// </para>
        /// <note>
        /// Any <b>".sh"</b> file extensions will be removed for ease-of-use.
        /// </note>
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void InstallToolScripts(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Install", "Tools");
            node.Status = "install: Tools";

            // Upload any tool scripts to the neonKUBE bin folder, stripping
            // the [*.sh] file type (if present) and then setting execute
            // permissions.

            var toolsFolder = KubeHelper.Resources.GetDirectory("/Tools");      // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121

            foreach (var file in toolsFolder.GetFiles())
            {
                var targetName = file.Name;

                if (Path.GetExtension(targetName) == ".sh")
                {
                    targetName = Path.GetFileNameWithoutExtension(targetName);
                }

                using (var toolStream = file.OpenStream())
                {
                    node.UploadText(LinuxPath.Combine(KubeNodeFolders.Bin, targetName), toolStream, permissions: "744");
                }
            }
        }

        /// <summary>
        /// Installs the Helm charts as a single ZIP archive written to the 
        /// neonKUBE Helm folder.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public static void InstallHelmArchive(NodeSshProxy<NodeDefinition> node, Action<string> logWriter = null)
        {
            using (var ms = new MemoryStream())
            {
                var helmFolder = KubeHelper.Resources.GetDirectory("/Helm");    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121

                helmFolder.Zip(ms, zipOptions: StaticZipOptions.LinuxLineEndings);

                ms.Seek(0, SeekOrigin.Begin);
                node.Upload(KubeNodeFolders.Helm, ms, permissions: "660");
            }
        }
    }
}
