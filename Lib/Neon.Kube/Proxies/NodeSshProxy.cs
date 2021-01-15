//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.cs
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
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

// $todo(jefflill):
//
// The download methods don't seem to be working for paths like [/proc/meminfo].
// They return an empty stream.

// $todo(jefflill):
//
// Most of this code has been copied to the [Neon.SSH.NET] project under
// the [Neon.SSH] namespace.  There's just a tiny bit of extra functionality
// implemented by this and the derived [LinuxSshProxy] class.
//
// We should convert this class to inherit from the [Neon.SSH.NET] class
// so we don't have to maintain duplicate code.
//
//      https://github.com/nforgeio/neonKUBE/issues/1006

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
    /// of Linux and it makes some global changes including disabling SUDO password prompts
    /// for all users as well as creating some global directories.
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
    /// Call <see cref="LinuxSshProxy{TMetadata}.Dispose()"/> or <see cref="LinuxSshProxy{TMetadata}.Disconnect()"/>
    /// to close the connection.
    /// </para>
    /// <note>
    /// You can use <see cref="Clone()"/> to make a copy of a proxy that can be
    /// used to perform parallel operations against the same machine.
    /// </note>
    /// </remarks>
    /// <threadsafety instance="false"/>
    public class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        private static readonly Regex   idempotentRegex = new Regex(@"[a-z0-9\.-/]+", RegexOptions.IgnoreCase);

        /// <summary>
        /// Constructs a <see cref="LinuxSshProxy{TMetadata}"/>.
        /// </summary>
        /// <param name="name">The display name for the server.</param>
        /// <param name="address">The private cluster IP address for the server.</param>
        /// <param name="credentials">The credentials to be used for establishing SSH connections.</param>
        /// <param name="port">Optionally overrides the standard SSH port (22).</param>
        /// <param name="logWriter">The optional <see cref="TextWriter"/> where operation logs will be written.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="name"/> or if <paramref name="credentials"/> is <c>null</c>.
        /// </exception>
        public NodeSshProxy(string name, IPAddress address, SshCredentials credentials, int port = NetworkPorts.SSH, TextWriter logWriter = null)
            : base(name, address, credentials, port, logWriter)
        {
        }

        /// <summary>
        /// The associated <see cref="ClusterProxy"/> or <c>null</c>.
        /// </summary>
        public ClusterProxy Cluster { get; internal set; }

        /// <summary>
        /// Returns the connection information for SSH.NET.
        /// </summary>
        /// <returns>The connection information.</returns>
        private ConnectionInfo GetConnectionInfo()
        {
            var address = string.Empty;
            var port    = SshPort;

            if (Cluster?.HostingManager != null)
            {
                var ep = Cluster.HostingManager.GetSshEndpoint(this.Name);

                address = ep.Address;
                port    = ep.Port;
            }
            else
            {
                address = Address.ToString();
            }

            var connectionInfo = new ConnectionInfo(address, port, credentials.Username, GetAuthenticationMethod(credentials))
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
        /// Returns a clone of the SSH proxy.  This can be useful for situations where you
        /// need to be able to perform multiple SSH/SCP operations against the same
        /// machine in parallel.
        /// </summary>
        /// <returns>The cloned <see cref="NodeSshProxy{TMetadata}"/>.</returns>
        public new NodeSshProxy<TMetadata> Clone()
        {
            var clone = new NodeSshProxy<TMetadata>(Name, Address, credentials, SshPort, logWriter);

            LinuxSshProxy<TMetadata>.CloneTo(this, clone);

            return clone;
        }

        /// <summary>
        /// Indicates whether an idempotent action has been completed.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        /// <returns><c>true</c> when the action has already been completed.</returns>
        public bool GetIdempotentState(string actionId)
        {
            return FileExists(LinuxPath.Combine(KubeNodeFolders.State, actionId));
        }

        /// <summary>
        /// Explicitly indicates that an idempotent action has been completed
        /// on the node.
        /// </summary>
        /// <param name="actionId">The action ID.</param>
        public void SetIdempotentState(string actionId)
        {
            SudoCommand($"mkdir -p {KubeNodeFolders.State} && touch {KubeNodeFolders.State}/{actionId}", RunOptions.FaultOnError);
        }

        /// <summary>
        /// Invokes a named action on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The action to be performed.</param>
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
        /// on the node at <see cref="KubeNodeFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public bool InvokeIdempotent(string actionId, Action action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            if (action.GetMethodInfo().ReturnType != typeof(void))
            {
                // Ensure that a "void" async method isn't being passed because that
                // would be treated as fire-and-forget and is not what developers
                // will expect.

                throw new ArgumentException($"Possible async delegate passed to [{nameof(InvokeIdempotent)}()]", nameof(action));
            }

            var stateFolder = KubeNodeFolders.State;
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
        /// Invokes a named action asynchronously on the node if it has never been been performed
        /// on the node before.
        /// </summary>
        /// <param name="actionId">The node-unique action ID.</param>
        /// <param name="action">The asynchronous action to be performed.</param>
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
        /// on the node at <see cref="KubeNodeFolders.State"/><b>/ACTION-ID</b>.
        /// To ensure idempotency, this method first checks for the existance of
        /// this file and returns immediately without invoking the action if it is 
        /// present.
        /// </para>
        /// </remarks>
        public async Task<bool> InvokeIdempotentAsync(string actionId, Func<Task> action)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(actionId), nameof(actionId));
            Covenant.Requires<ArgumentException>(idempotentRegex.IsMatch(actionId), nameof(actionId));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            var stateFolder = KubeNodeFolders.State;
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

            await action();

            if (!IsFaulted)
            {
                SudoCommand($"touch {statePath}");
            }

            return true;
        }

        /// <summary>
        /// Ensures that the node operating system and version is supported for a neonKUBE
        /// cluster.  This faults the nodeproxy on faliure.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        /// <returns><c>true</c> if the operation system is supported.</returns>
        public bool VerifyNodeOS(Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Check", "OS");
            Status = "check: OS";

            // $todo(jefflill): We're currently hardcoded to Ubuntu 20.04.x

            if (!OsName.Equals("Ubuntu", StringComparison.InvariantCultureIgnoreCase) || OsVersion < Version.Parse("20.04"))
            {
                Fault("Expected: Ubuntu 20.04+");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs low-level initialization of a cluster   This is applied one time to
        /// Hyper-V and XenServer/XCP-ng node templates when they are created and at cluster
        /// creation time for cloud and bare metal clusters.  The node must already be running.
        /// </summary>
        /// <param name="sshPassword">The current <b>sysadmin</b> password.</param>
        /// <param name="updateDistribution">Optionally upgrade the node's Linux distribution.  This defaults to <c>false</c>.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public void Initialize(string sshPassword, bool updateDistribution = false, Action<string> logWriter = null)
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
            Status = $"login: [{KubeConst.SysAdminUser}]";

            WaitForBoot();
            DisableSnap(logWriter: logWriter);
            ConfigureApt(logWriter: logWriter);
            InstallAptPackages(logWriter: logWriter);

            if (updateDistribution)
            {
                UpdateLinux();
            }
        }

        /// <summary>
        /// Installs the required Apt packages.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void InstallAptPackages(Action<string> logWriter = null)
        {
            Status = "install: apt packages";
            KubeHelper.LogStatus(logWriter, "Install", "Apt packages");

            InvokeIdempotent("node/apt-packages",
                () =>
                {
                    var script =
@"
apt-get update
apt-get install -yq --allow-downgrades zip secure-delete
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Updates the Linux distribution.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void UpdateLinux(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/update-linux",
                () =>
                {
                    Status = "update: linux";
                    KubeHelper.LogStatus(logWriter, "Update", "Linux");

                    SudoCommand("apt-get dist-upgrade -yq");
                });
        }

        /// <summary>
        /// Disables the Linux memory swap file.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void DisableSwap(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/swap-disable",
                () =>
                {
                    Status = "disable: swap";
                    KubeHelper.LogStatus(logWriter, "Disable", "swap");

                    // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img] line.

                    KubeHelper.LogStatus(logWriter, "Disable", "swap");
                    Status = "disable: swap";

                    var sbFsTab = new StringBuilder();

                    using (var reader = new StringReader(DownloadText("/etc/fstab")))
                    {
                        foreach (var line in reader.Lines())
                        {
                            if (!line.Contains("/swap.img"))
                            {
                                sbFsTab.AppendLine(line);
                            }
                        }
                    }

                    UploadText("/etc/fstab", sbFsTab, permissions: "644", owner: "root:root");
                });
        }

        /// <summary>
        /// Installs hypervisor guest integration services.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void InstallGuestIntegrationServices(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/guest-integration",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Install", "Guest integration services");
                    Status = "install: guest integration services";

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
                    SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables DHCP.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void DisableDhcp(Action<string> logWriter = null)
        {
            InvokeIdempotent("node-dhcp",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Disable", "DHCP");
                    Status = "disable: DHCP";

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
                    SudoCommand(CommandBundle.FromScript(initNetPlanScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables <b>cloud-init</b>.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void DisableCloudInit(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/cloud-init",
                () =>
                {
                KubeHelper.LogStatus(logWriter, "Disable", "cloud-init");
                Status = "disable: cloud-init";

                var disableCloudInitScript =
$@"
touch /etc/cloud/cloud-init.disabled
";
                SudoCommand(CommandBundle.FromScript(disableCloudInitScript), RunOptions.FaultOnError);
            });
        }

        /// <summary>
        /// Cleans a node by removing unnecessary package manager metadata, cached DHCP information, etc.
        /// and then fills unreferenced file system blocks and nodes with zeros so the disk image will
        /// compress better.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void Clean(Action<string> logWriter = null)
        {
            KubeHelper.LogStatus(logWriter, "Clean", "VM");
            Status = "clean: VM";

            var cleanScript =
@"#!/bin/bash
cloud-init clean
apt-get clean
rm -rf /var/lib/dhcp/*
sfill -fllz /
";
            SudoCommand(CommandBundle.FromScript(cleanScript), RunOptions.FaultOnError);
        }

        /// <summary>
        /// Customizes the OpenSSH configuration on a 
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void ConfigureOpenSsh(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/openssh",
                () =>
                {
                    // Upload the OpenSSH server configuration and restart OpenSSH.

                    KubeHelper.LogStatus(logWriter, "Configure", "OpenSSH");
                    Status = "configure: OpenSSH";

                    UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
                    SudoCommand("systemctl restart sshd");
                });
        }

        /// <summary>
        /// Removes unnecessary packages.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void CleanPackages(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/clean-packages",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Remove", "Unnecessary packages");
                    Status = "Remove: Unnecessary packages";

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
            SudoCommand(CommandBundle.FromScript(removePackagesScript);
#endif
                });
        }

        /// <summary>
        /// Configures the APY package manager.
        /// </summary>
        /// <param name="packageManagerRetries">Optionally specifies the packager manager retries (defaults to <b>5</b>).</param>
        /// <param name="allowPackageManagerIPv6">Optionally prevent the package manager from using IPv6 (defaults to <c>false</c>.</param>
        /// <param name="logWriter">Optional log writer action.</param>
        public void ConfigureApt(int packageManagerRetries = 5, bool allowPackageManagerIPv6 = false, Action<string> logWriter = null)
        {
            InvokeIdempotent("node/apt",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Configure", "[apt] package manager");
                    Status = "configure: [apt] package manager";

                    if (!allowPackageManagerIPv6)
                    {
                        // Restrict the [apt] package manager to using IPv4 to communicate
                        // with the package mirrors, since IPv6 doesn't work sometimes.

                        UploadText("/etc/apt/apt.conf.d/99-force-ipv4-transport", "Acquire::ForceIPv4 \"true\";");
                        SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-force-ipv4-transport", RunOptions.FaultOnError);
                    }

                    // Configure [apt] to retry.

                    UploadText("/etc/apt/apt.conf.d/99-retries", $"APT::Acquire::Retries \"{packageManagerRetries}\";");
                    SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-retries", RunOptions.FaultOnError);

                    // We're going to disable apt updating services so we can control when this happens.

                    var disableAptServices =
@"#------------------------------------------------------------------------------
# Disable the [apt-timer] and [apt-daily] services.  We're doing this 
# for two reasons:
#
#   1. These services interfere with with [apt-get] usage during
# cluster setup and is also likely to interfere with end-user
# configuration activities as well.
#
#   2. Automatic updates for production and even test clusters is
# just not a great idea.  You just don't want a random update
# applied in the middle of the night which might cause trouble.
#
# We're going to implement our own cluster updating machanism
# that will be smart enough to update the nodes such that the
# impact on cluster workloads will be limited.

systemctl stop apt-daily.timer
systemctl mask apt-daily.timer

systemctl stop apt-daily.service
systemctl mask apt-daily.service

# It may be possible for the auto updater to already be running so we'll
# wait here for it to release any lock files it holds.

while fuser /var/{{lib /{{dpkg,apt/lists}},cache/apt/archives}}/lock; do
    sleep 1
done";
                    SudoCommand(CommandBundle.FromScript(disableAptServices), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables the SNAP package manasger.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void DisableSnap(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/snap",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Disable", "[snapd.service]");
                    Status = "disable: [snapd.service]";

                    var disableSnapScript =
@"# Stop and mask [snapd.service] if it's not already masked.

systemctl status --no-pager snapd.service

if [ $? ]; then
    systemctl stop snapd.service
    systemctl mask snapd.service
fi
";
                    SudoCommand(CommandBundle.FromScript(disableSnapScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>neon-init</b> service which is a cloud-init like service we
        /// use to configure the network and credentials for VMs hosted in non-cloud
        /// hypervisors.
        /// </summary>
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
        public void InstallNeonInit(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/neon-init",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Install", "neon-init.service");
                    Status = "install: neon-init.service";

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
# http://www.apache.org/licenses/LICENSE-2.0
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
# or XenServer host machine.
#
#       3. Setup inserts the VFD into the VM's DVD drive and starts the VM.
#
#       4. The VM boots, eventually running this script (via the
#          [neon-init] service).
#
#       5. This script checks whether a DVD is present, mounts
# it and checks it for the [neon-init.sh] script.
#
#       6. If the DVD and script file are present, this service will
# execute the script via Bash, peforming any required custom setup.
# Then this script creates the [/etc/neon-init] file which 
# prevents the service from doing anything during subsequent node 
# reboots.
#
#       7. The service just exits if the DVD and/or script file are 
# not present.  This shouldn't happen in production but is useful
# for script debugging.

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
                    SudoCommand(CommandBundle.FromScript(neonNodePrepScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Create the node folders required by neoneKUBE.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void CreateKubeFolders(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/folders",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Create", "Cluster folders");
                    Status = "create: cluster folders";

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
                    SudoCommand(CommandBundle.FromScript(folderScript), RunOptions.LogOnErrorOnly);
                });
        }

        /// <summary>
        /// <para>
        /// Installs the tool scripts, making them executable.
        /// </para>
        /// <note>
        /// Any <b>".sh"</b> file extensions will be removed for ease-of-use.
        /// </note>
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void InstallToolScripts(Action<string> logWriter = null)
        {
            InvokeIdempotent("node/tool-scripts",
                () =>
                {
                    KubeHelper.LogStatus(logWriter, "Install", "Tools");
                    Status = "install: Tools";

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
                            UploadText(LinuxPath.Combine(KubeNodeFolders.Bin, targetName), toolStream, permissions: "744");
                        }
                    }
                });
        }

        /// <summary>
        /// Installs the <b>CRI-O</b> container runtime.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void InstallCriO(Action<string> logWriter = null)
        {
            var setupScript =
$@"
# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
# http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

# Create the .conf file to load the modules at bootup
cat <<EOF | sudo tee /etc/modules-load.d/crio.conf
overlay
br_netfilter
EOF

sudo modprobe overlay
sudo modprobe br_netfilter

sysctl --system

OS=xUbuntu_20.04
VERSION={KubeVersions.CrioVersion}

cat <<EOF | sudo tee /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
deb https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${{OS}}/ /
EOF
cat <<EOF | sudo tee /etc/apt/sources.list.d/devel:kubic:libcontainers:stable:cri-o:${{VERSION}}.list
deb http://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/${{VERSION}}/${{OS}}/ /
EOF

curl -L https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${{OS}}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers.gpg add -
curl -L https://download.opensuse.org/repositories/devel:kubic:libcontainers:stable:cri-o:${{VERSION}}/${{OS}}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers-cri-o.gpg add -

apt-get update -y
apt-get install -y cri-o cri-o-runc

cat <<EOF | sudo tee /etc/containers/registries.conf
unqualified-search-registries = [ ""$<neon-branch-registry>"", ""docker.io"", ""quay.io"", ""registry.access.redhat.com"", ""registry.fedoraproject.org""]

[[registry]]
prefix = ""$<neon-branch-registry>""
insecure = false
blocked = false
location = ""$<neon-branch-registry>""
[[registry.mirror]]
location = ""registry.neon-system""

[[registry]]
prefix = ""docker.io""
insecure = false
blocked = false
location = ""docker.io""
[[registry.mirror]]
location = ""registry.neon-system""

[[registry]]
prefix = ""quay.io""
insecure = false
blocked = false
location = ""quay.io""
[[registry.mirror]]
location = ""registry.neon-system""
EOF

cat <<EOF | sudo tee /etc/crio/crio.conf.d/01-cgroup-manager.conf
[crio.runtime]
cgroup_manager = ""systemd""
EOF

# We need to do a [daemon-reload] so systemd will be aware of the new unit drop-in.

systemctl disable crio
systemctl daemon-reload

# Configure CRI-O to start on boot and then restart it to pick up the new options.

systemctl enable crio
systemctl restart crio

# Prevent the package manager from automatically upgrading the container runtime.

set +e      # Don't exit if the next command fails
apt-mark hold crio cri-o-runc
";
            InvokeIdempotent("cri-o",
                () =>
                {
                    Status = "setup: cri-o";
                    SudoCommand(setupScript, RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the Helm charts as a single ZIP archive written to the 
        /// neonKUBE Helm folder.
        /// </summary>
        /// <param name="logWriter">Optional log writer action.</param>
        public void InstallHelmArchive(Action<string> logWriter = null)
        {
            using (var ms = new MemoryStream())
            {
                var helmFolder = KubeHelper.Resources.GetDirectory("/Helm");    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121

                helmFolder.Zip(ms, zipOptions: StaticZipOptions.LinuxLineEndings);

                ms.Seek(0, SeekOrigin.Begin);
                Upload(KubeNodeFolders.Helm, ms, permissions: "660");
            }
        }
    }
}
