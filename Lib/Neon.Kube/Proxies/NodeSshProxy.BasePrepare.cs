//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.BasePrepare.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

// This file includes node configuration methods executed while preparing a
// node base image or performing live low-level initialization of a baremetal
// node.

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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube 
{
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        /// <summary>
        /// Performs low-level initialization of a cluster node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="upgradeLinux">Optionally upgrade the node's Linux distribution (defaults to <c>false</c>).</param>
        /// <param name="patchLinux">Optionally apply any available Linux security patches (defaults to <c>true</c>).</param>
        public void BaseInitialize(ISetupController controller, bool upgradeLinux = false, bool patchLinux = true)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            // Wait for boot/connect.

            controller.LogProgress(this, verb: "login", message: $"[{KubeConst.SysAdminUser}]");

            WaitForBoot();
            VerifyNodeOS(controller);
            BaseDisableSwap(controller);
            BaseInstallToolScripts(controller);
            BaseConfigureDebianFrontend(controller);
            UpdateRootCertificates();
            BaseInstallPackages(controller);
            BaseBlacklistFloppy(controller);
            BaseConfigureApt(controller);
            BaseConfigureBashEnvironment(controller);
            BaseConfigureDnsIPv4Preference(controller);
            BaseRemoveSnap(controller);
            BaseRemovePackages(controller);

            if (patchLinux)
            {
                BasePatchLinux(controller);
            }

            BaseCreateKubeFolders(controller);

            if (upgradeLinux)
            {
                BaseUpgradeLinuxDistribution(controller);
            }
        }

        /// <summary>
        /// Blacklists the Kernel floppy module so it will be disabled when
        /// the machine is rebooted.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseBlacklistFloppy(ISetupController controller)
        {
            InvokeIdempotent("base/blacklist-floppy",
                () =>
                {
                    controller.LogProgress(this, verb: "blacklist", message: "floppy drive");

                    var floppyScript =
@"
set -euo pipefail

# Blacklist the floppy drive:

echo ""blacklist floppy"" | sudo tee /etc/modprobe.d/blacklist-floppy.conf
dpkg-reconfigure initramfs-tools 

# This seems to cause the kernel to restart sometimes, so we're
# not going to actually remove the module in the current session.
# The commands above will ensure that the module will be disabled
# after the next reboot.

# rmmod -v floppy
";
                    SudoCommand(CommandBundle.FromScript(floppyScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures the Debian frontend terminal to non-interactive.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseConfigureDebianFrontend(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/debian-frontend",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "tty");

                    // We need to append [DEBIAN_FRONTEND] to the [/etc/environment] file but
                    // we haven't installed [zip/unzip] yet so we can't use a command bundle.
                    // We'll just use [tee] in this case. 

                    SudoCommand("echo DEBIAN_FRONTEND=noninteractive | tee -a /etc/environment", RunOptions.Defaults | RunOptions.FaultOnError);
                    SudoCommand("echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections", RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Ubuntu defaults DNS to prefer IPv6 lookups over IPv4 which can cause
        /// performance problems.  This method reconfigures DNS to favor IPv4.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseConfigureDnsIPv4Preference(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/dns-ipv4",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "dns ipv4 preference");

                    var script =
@"
set -euo pipefail

#------------------------------------------------------------------------------
# We need to modify how [getaddressinfo] handles DNS lookups 
# so that IPv4 lookups are preferred over IPv6.  Ubuntu prefers
# IPv6 lookups by default.  This can cause performance problems 
# because in most situations right now, the server would be doing
# 2 DNS queries, one for AAAA (IPv6) which will nearly always 
# fail (at least until IPv6 is more prevalent) and then querying
# for the for A (IPv4) record.
#
# This can also cause issues when the server is behind a NAT.
# I ran into a situation where [apt-get update] started failing
# because one of the archives had an IPv6 address in addition to
# an IPv4.  Here's a note about this issue:
#
#       http://ubuntuforums.org/showthread.php?t=2282646
#
# We're going to uncomment the line below in [gai.conf] and
# change it to the following line to prefer IPv4.
#
#       #precedence ::ffff:0:0/96  10
#       precedence ::ffff:0:0/96  100
#
# Note that this does not completely prevent the resolver from
# returning IPv6 addresses.  You'll need to prvent this on an
# application by application basis, like using the [curl -4] option.

sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures the Debian frontend terminal to non-interactive.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseConfigureBashEnvironment(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/bash-environment",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "environment variables");

                    var script =
@"
set -euo pipefail
echo '. /etc/environment' > /etc/profile.d/env.sh
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the required <b>base image</b> packages.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseInstallPackages(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/base-packages",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "base packages");

                    // Install the packages.  Note that we haven't added our tool folder to the PATH 
                    // yet, so we'll use the fully qualified path to [safe-apt-get].

                    SudoCommand($"{KubeNodeFolder.Bin}/safe-apt-get update", RunOptions.Defaults | RunOptions.FaultOnError);
                    SudoCommand($"{KubeNodeFolder.Bin}/safe-apt-get install -yq apt-cacher-ng ntp secure-delete sysstat zip", RunOptions.Defaults | RunOptions.FaultOnError);

                    // $note(jefflill):
                    //
                    // I've seen some situations after a reboot where the machine complains about
                    // running out of entropy.  Apparently, modern CPUs have an instruction that
                    // returns cryptographically random data, but these CPUs weren't available
                    // until 2015 so our old HP SL 385 G10 XenServer machines won't support this.
                    //
                    // An reasonable alternative is [haveged]:
                    //   
                    //       https://wiki.archlinux.org/index.php/Haveged
                    //       https://www.digitalocean.com/community/tutorials/how-to-setup-additional-entropy-for-cloud-servers-using-haveged
                    //
                    // This article warns about using this though:
                    //
                    //       https://lwn.net/Articles/525459/
                    //
                    // The basic problem is that headless servers generally have very poor entropy
                    // sources because there's no mouse, keyboard, or active video card.  Outside
                    // of the new CPU instruction, the only sources are the HDD and network drivers.
                    // [haveged] works by timing running code at very high resolution and hoping to
                    // see execution time jitter and then use that as an entropy source.

                    // $note(jefflill):
                    //
                    // The official [haveged] releases before [1.9.8-4ubuntu3] have this bug that
                    // prevents [haveged] from running in a container.  We're also seeing (transient?)
                    // problems when installing this package on WSL2.
                    //
                    //      https://bugs.launchpad.net/ubuntu/+source/haveged/+bug/1894877
                    //      https://launchpad.net/ubuntu/+source/haveged/1.9.8-4ubuntu3
                    //
                    // It appears that WSL2 preloads entropy from the Windows host when the distro
                    // boots, so we should be OK without [haveged] in this case.
                    //
                    //      https://github.com/Microsoft/WSL/issues/4416
                    //      https://github.com/microsoft/WSL/issues/1789
                    //
                    // It looks like Linux kernels beginning with v5.6 integrate the HAVEGED algorithm
                    // directly, so we don't need to install the [haveged] service in this case:
                    //
                    //      https://github.com/jirka-h/haveged/blob/master/README.md

                    if (this.KernelVersion < new Version(5, 6, 0))
                    {
                        SudoCommand($"{KubeNodeFolder.Bin}/safe-apt-get install -yq haveged", RunOptions.Defaults | RunOptions.FaultOnError);
                    }
                });
        }

        /// <summary>
        /// Updates Linux by applying just the outstanding security updates.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BasePatchLinux(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/patch-linux",
                () =>
                {
                    controller.LogProgress(this, verb: "patch", message: "linux");
                    PatchLinux();
                });
        }

        /// <summary>
        /// Upgrades the Linux distribution on the node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseUpgradeLinuxDistribution(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/upgrade-linux",
                () =>
                {
                    controller.LogProgress(this, verb: "upgrade", message: "linux distribution");
                    UpgradeLinuxDistribution();
                });
        }

        /// <summary>
        /// Disables the Linux memory swap file.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseDisableSwap(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/swap-disable",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "swap");

                    // Disable SWAP by editing [/etc/fstab] to remove the [/swap.img...] line.

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
        /// <param name="controller">The setup controller.</param>
        public void BaseInstallGuestIntegrationServices(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            // This currently applies only to on-premise hypervisors.

            if (!KubeHelper.IsOnPremiseHypervisorEnvironment(hostingEnvironment))
            {
                return;
            }

            InvokeIdempotent("base/guest-integration",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "guest integration");

                    var guestServicesScript =
$@"#!/bin/bash
set -euo pipefail

cat <<EOF >> /etc/initramfs-tools/modules
hv_vmbus
hv_storvsc
hv_blkvsc
hv_netvsc
EOF

{KubeNodeFolder.Bin}/safe-apt-get install -yq linux-virtual linux-cloud-tools-virtual linux-tools-virtual
update-initramfs -u
";
                    SudoCommand(CommandBundle.FromScript(guestServicesScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables DHCP.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseDisableDhcp(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("base/dhcp",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "dhcp");

                    var initNetPlanScript =
$@"
set -euo pipefail

rm -rf /etc/netplan/*

cat <<EOF > /etc/netplan/no-dhcp.yaml
# This file is used to disable the network when a new VM is created 
# from a template is booted.  The [neon-init] service handles network
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
                    SudoCommand(CommandBundle.FromScript(initNetPlanScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables <b>cloud-init</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseDisableCloudInit(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            // Do this only for non-cloud environments.

            if (KubeHelper.IsCloudEnvironment(hostingEnvironment))
            {
                return;
            }

            InvokeIdempotent("base/cloud-init",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "cloud-init");

                    var disableCloudInitScript =
$@"
set -euo pipefail
mkdir -p /etc/cloud
touch /etc/cloud/cloud-init.disabled
";
                    SudoCommand(CommandBundle.FromScript(disableCloudInitScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Customizes the OpenSSH configuration on a 
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseConfigureOpenSsh(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/openssh",
                () =>
                {
                    // Upload the OpenSSH server configuration and restart OpenSSH.

                    controller.LogProgress(this, verb: "configure", message: "openssh");

                    UploadText("/etc/ssh/sshd_config", KubeHelper.OpenSshConfig);
                    SudoCommand("systemctl restart sshd", RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Removes any installed snaps as well as the entire snap infrastructure.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseRemoveSnap(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/remove-snap",
                () =>
                {
                    controller.LogProgress(this, verb: "remove", message: "snap");

                    var script =
@"
set -euo pipefail

safe-apt-get purge snapd -yq

rm -rf ~/snap
rm -rf /var/cache/snapd
rm -rf /snap
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Removes unneeded packages.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseRemovePackages(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/remove-packages",
                () =>
                {
                    controller.LogProgress(this, verb: "remove", message: "unneeded packages");

                    // Purge unneeded packages.

var removePackagesScript =
$@"
set -euo pipefail

{KubeNodeFolder.Bin}/safe-apt-get purge -y \
    git git-man \
    iso-codes \
    locales \
    manpages man-db \
    python3-twisted \
    snapd \
    vim vim-runtime vim-tiny

{KubeNodeFolder.Bin}/safe-apt-get autoremove -y
";
                    SudoCommand(CommandBundle.FromScript(removePackagesScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures the APT package manager.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="packageManagerRetries">Optionally specifies the packager manager retries (defaults to <b>5</b>).</param>
        /// <param name="allowPackageManagerIPv6">Optionally prevent the package manager from using IPv6 (defaults to <c>false</c>.</param>
        public void BaseConfigureApt(ISetupController controller, int packageManagerRetries = 5, bool allowPackageManagerIPv6 = false)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/apt",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "package manager");

                    if (!allowPackageManagerIPv6)
                    {
                        // Restrict the [apt] package manager to using IPv4 to communicate
                        // with the package mirrors, since IPv6 doesn't work sometimes.

                        UploadText("/etc/apt/apt.conf.d/99-force-ipv4-transport", "Acquire::ForceIPv4 \"true\";");
                        SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-force-ipv4-transport", RunOptions.Defaults | RunOptions.FaultOnError);
                    }

                    // Configure [apt] to retry.

                    UploadText("/etc/apt/apt.conf.d/99-retries", $"APT::Acquire::Retries \"{packageManagerRetries}\";");
                    SudoCommand("chmod 644 /etc/apt/apt.conf.d/99-retries", RunOptions.Defaults | RunOptions.FaultOnError);

                    // We're going to disable apt updating services so we can control when this happens.

                    var disableAptServices =
@"
set -euo pipefail

#------------------------------------------------------------------------------
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
# We're going to implement our own cluster updating machanism
# that will be smart enough to update the nodes such that the
# impact on cluster workloads will be limited.

systemctl stop apt-daily.timer
systemctl mask apt-daily.timer

systemctl stop apt-daily.service
systemctl mask apt-daily.service

# It may be possible for the auto updater to already be running so we'll
# wait here for it to release any lock files it holds.

while fuser /var/lib/dpkg/lock >/dev/null 2>&1; do
    sleep 1
done

#------------------------------------------------------------------------------
# Update the APT config to disable updates there as well.
cat > /etc/apt/apt.conf.d/20auto-upgrades <<EOF
APT::Periodic::Update-Package-Lists ""0"";
APT::Periodic::Download-Upgradeable-Packages ""0"";
APT::Periodic::AutocleanInterval ""0"";
APT::Periodic::Unattended-Upgrade ""1"";
EOF
";
                    SudoCommand(CommandBundle.FromScript(disableAptServices), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>neon-init</b> service which is a poor man's cloud-init like 
        /// service we use to configure the network and credentials for VMs hosted in non-cloud
        /// hypervisors.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
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
        /// drive and when present, the script will be executed and the [/etc/neon-init/ready]
        /// file will be created to indicate that the service no longer needs to do this for
        /// subsequent reboots.
        /// </para>
        /// <note>
        /// The script won't create the [/etc/neon-init] when the script ISO doesn't exist 
        /// for debugging purposes.
        /// </note>
        /// </remarks>
        public void BaseInstallNeonInit(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/neon-init",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "neon-init.service");

                    var neonNodePrepScript =
$@"# Ensure that the neon binary folder exists.

mkdir -p {KubeNodeFolder.Bin}

# Create the systemd unit file.

cat <<EOF > /etc/systemd/system/neon-init.service

[Unit]
Description=neonKUBE one-time node preparation service 
After=systemd-networkd.service

[Service]
Type=oneshot
ExecStart={KubeNodeFolder.Bin}/neon-init
RemainAfterExit=false
StandardOutput=journal+console

[Install]
WantedBy=multi-user.target
EOF

# Create the service script.

cat <<EOF > {KubeNodeFolder.Bin}/neon-init
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:	        neon-init
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
#
# NOTE: Ubuntu 22.04 seems to have removed the [/dev/dvd] device but 
#       [/dev/cdrom] works, su we're swiching to that.

# Run the prep script only once.

if [ -f /etc/neon-init/ready ] ; then
    echo ""INFO: Machine is already ready.""
    exit 0
fi

# Create a mount point for the DVD.

if ! mkdir -p /media/neon-init ; then
    echo ""ERROR: Cannot create DVD mount point.""
    exit 1
fi

# Wait up to 120 seconds for for the DVD to be discovered.  It can
# take some time for this to happen.

mounted=$false

for i in {{1..24}}; do

    # Sleep for 5 seconds.  We're doing this first to give Linux
    # a chance to discover the DVD and then this will act as a
    # retry interval.  24 iterations at 5 seconds each is 120 seconds.

    sleep 5

    # Try mounting the DVD.

    if mount /dev/cdrom /media/neon-init ; then
        
        mounted=$true
        break
    fi

    echo ""WARNING: No DVD is present (yet).""

done

if ! $mounted; then
    echo ""WARNING: No DVD is present: exiting""
    exit 1
fi

# Check for the [neon-init.sh] script and execute it.

if [ ! -f /media/neon-init/neon-init.sh ] ; then
    echo ""WARNING: No [neon-init.sh] script is present on the DVD: exiting""
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

# Disable [neon-init] so it does nothing the next time it's launched.

mkdir -p /etc/neon-init
touch /etc/neon-init/ready
EOF

chmod 744 {KubeNodeFolder.Bin}/neon-init

# Configure [neon-init] to start at boot.

systemctl enable neon-init
systemctl daemon-reload

# ---------------------------------------------------
";
                    SudoCommand(CommandBundle.FromScript(neonNodePrepScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Create the node folders required by neoneKUBE.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void BaseCreateKubeFolders(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/folders",
                () =>
                {
                    controller.LogProgress(this, verb: "create", message: "node folders");

                    var folderScript =
$@"
set -euo pipefail

mkdir -p {KubeNodeFolder.Bin}
chmod 750 {KubeNodeFolder.Bin}

mkdir -p {KubeNodeFolder.Config}
chmod 750 {KubeNodeFolder.Config}

mkdir -p {KubeNodeFolder.Setup}
chmod 750 {KubeNodeFolder.Setup}

mkdir -p {KubeNodeFolder.Helm}
chmod 750 {KubeNodeFolder.Helm}

mkdir -p {KubeNodeFolder.State}
chmod 750 {KubeNodeFolder.State}

mkdir -p {KubeNodeFolder.State}/setup
chmod 750 {KubeNodeFolder.State}/setup

mkdir -p {KubeNodeFolder.NeonRun}
chmod 740 {KubeNodeFolder.NeonRun}
";
                    SudoCommand(CommandBundle.FromScript(folderScript), RunOptions.Defaults | RunOptions.FaultOnError);
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
        /// <param name="controller">The setup controller.</param>
        public void BaseInstallToolScripts(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/tool-scripts",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "tools (base)");

                    // Upload any tool scripts to the neonKUBE bin folder, stripping
                    // the [*.sh] file type (if present) and then setting execute
                    // permissions.

                    var scriptsFolder = KubeHelper.Resources.GetDirectory("/Tools");    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121

                    foreach (var file in scriptsFolder.GetFiles())
                    {
                        var targetName = file.Name;

                        if (Path.GetExtension(targetName) == ".sh")
                        {
                            targetName = Path.GetFileNameWithoutExtension(targetName);
                        }

                        using (var toolStream = file.OpenStream())
                        {
                            UploadText(LinuxPath.Combine(KubeNodeFolder.Bin, targetName), toolStream, permissions: "744");
                        }
                    }
                });
        }
    }
}
