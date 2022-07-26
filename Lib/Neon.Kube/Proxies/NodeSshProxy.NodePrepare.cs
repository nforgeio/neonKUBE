//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.NodePrepare.cs
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

// This file includes node configuration methods executed while setting
// up a neonKUBE cluster.

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
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
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
        /// Removes the Linux swap file if present.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void RemoveSwapFile(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/swap-remove",
                () =>
                {
                    SudoCommand("rm -f /swap.img");
                });
        }

        /// <summary>
        /// Installs the neonKUBE related tools to the <see cref="KubeNodeFolder.Bin"/> folder.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallTools(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/tools",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "tools (node)");

                    foreach (var file in KubeHelper.Resources.GetDirectory("/Tools").GetFiles())
                    {
                        // Upload each tool script, removing the extension.

                        var targetName = LinuxPath.GetFileNameWithoutExtension(file.Path);
                        var targetPath = LinuxPath.Combine(KubeNodeFolder.Bin, targetName);

                        UploadText(targetPath, file.ReadAllText(), permissions: "774", owner: KubeConst.SysAdminUser);
                    }
                });
        }

        /// <summary>
        /// <para>
        /// Configures a node's host public SSH key during node provisioning.
        /// </para>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void ConfigureSshKey(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            // Write the SSHD subconfig file and configure the SSH certificate
            // credentials for [sysadmin] on the node.

            InvokeIdempotent("prepare/ssh",
                () =>
                {
                    CommandBundle bundle;

                    // Upload our custom SSH config files.

                    controller.LogProgress(this, verb: "update", message: "ssh config");

                    var configText = KubeHelper.OpenSshConfig;

                    UploadText("/etc/ssh/sshd_config", NeonHelper.ToLinuxLineEndings(configText), permissions: "644");

                    var subConfigText = KubeHelper.GetOpenSshPrepareSubConfig(allowPasswordAuth: true);

                    UploadText("/etc/ssh/sshd_config.d/50-neonkube.conf", NeonHelper.ToLinuxLineEndings(subConfigText), permissions: "644"); 

                    // Here's some information explaining what how this works:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    controller.LogProgress(this, verb: "generate", message: "ssh keys");

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

                    RunCommand(bundle);

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
                    SudoCommand(bundle, RunOptions.FaultOnError);
                });

            // Verify that we can login with the new SSH private key and also verify that
            // the password still works.

            // $todo(jefflill):
            //
            // Key based authentication isn't working at the moment for some reason.  I'm
            // going to disable the login check for now and come back to this when we switch
            // to key based authentication for AWS.

#if DISABLED
            controller.LogProgress(this, verb: "verify", message: "ssh keys");

            Disconnect();
            UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, clusterLogin.SshKey.PrivatePEM));
            WaitForBoot();
#endif

            controller.LogProgress(this, verb: "verify", message: "ssh password");

            Disconnect();
            UpdateCredentials(SshCredentials.FromUserPassword(KubeConst.SysAdminUser, clusterLogin.SshPassword));
            WaitForBoot();
        }

        /// <summary>
        /// Disables the <b>snapd</b> service.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void DisableSnap(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/disable-snap",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "snapd.service");

                    //-----------------------------------------------------------------
                    // We're going to stop and mask the [snapd.service] if it's running
                    // because we don't want it to randomlly update apps on cluster nodes.

                    var disableSnapScript =
@"
# Stop and mask [snapd.service] when it's not already masked.

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
        /// Required NFS setup.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void ConfigureNFS(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/nfs",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "nfs");

                    //-----------------------------------------------------------------
                    // We need to install nfs-common tools for NFS to work.

                    var InstallNfsScript =
@"
set -euo pipefail

safe-apt-get update -y
safe-apt-get install -y nfs-common
";
                    SudoCommand(CommandBundle.FromScript(InstallNfsScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures <b>journald</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void ConfigureJournald(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("base/journald",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "journald filters");

                    var filterScript =
@"
# neonKUBE: Filter [rsyslog.service] log events we don't care about.

cat <<EOF > /etc/rsyslog.d/60-filter.conf
if $programname == ""systemd"" and ($msg startswith ""Created slice "" or $msg startswith ""Removed slice "") then stop
EOF

systemctl restart rsyslog.service
";
                    SudoCommand(CommandBundle.FromScript(filterScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Initializes a near virgin server with the basic capabilities required
        /// for a cluster node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void PrepareNode(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var hostingManager    = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);
            var clusterDefinition = cluster.Definition;

            InvokeIdempotent("base/prepare-node",
                () =>
                {
                    controller.LogProgress(this, verb: "prepare", message: "node");

                    controller.ThrowIfCancelled();
                    RemoveSwapFile(controller);

                    controller.ThrowIfCancelled();
                    NodeInstallTools(controller);

                    controller.ThrowIfCancelled();
                    BaseConfigureApt(controller, clusterDefinition.NodeOptions.PackageManagerRetries, clusterDefinition.NodeOptions.AllowPackageManagerIPv6);

                    controller.ThrowIfCancelled();
                    BaseConfigureOpenSsh(controller);

                    controller.ThrowIfCancelled();
                    DisableSnap(controller);

                    controller.ThrowIfCancelled();
                    ConfigureJournald(controller);

                    controller.ThrowIfCancelled();
                    ConfigureNFS(controller);
                });
        }

        /// <summary>
        /// Performs low-level node initialization during cluster setup.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInitialize(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("base/blacklist-floppy",
                () =>
                {
                    controller.LogProgress(this, verb: "blacklist", message: "floppy drive");

                    var floppyScript =
@"
set -euo pipefail

# We need to blacklist the floppy drive.  Not doing this can cause
# node failures:

rmmod floppy
echo ""blacklist floppy"" | tee /etc/modprobe.d/blacklist-floppy.conf
dpkg-reconfigure initramfs-tools
";
                    SudoCommand(CommandBundle.FromScript(floppyScript));
                });

            InvokeIdempotent("base/sysstat",
                () =>
                {
                    controller.LogProgress(this, verb: "enable", message: "sysstat");

                    var statScript =
@"
set -euo pipefail

# Enable system statistics collection (e.g. Page Faults,...)

sed -i '/^ENABLED=""false""/c\ENABLED=""true""' /etc/default/sysstat
";
                    SudoCommand(CommandBundle.FromScript(statScript));
                });

            var script =
$@"
#------------------------------------------------------------------------------
# Basic initialization

timedatectl set-timezone UTC

#------------------------------------------------------------------------------
# Configure the [apt] package manager to retry downloads up to 5 times.

echo 'APT::Acquire::Retries ""5"";' > /etc/apt/apt.conf.d/80-retries
chmod 644 /etc/apt/apt.conf.d/80-retries

#------------------------------------------------------------------------------
# We need to increase the number of file descriptors and also how much memory
# can be locked by root processes.  We're simply going to overwrite the default
# version of [/etc/security/limits.conf] with our own copy.
#
# Note that [systemd] ignores [limits.conf] when starting services, etc.  It
# has its own configuration which we'll update below, but [limits.conf] is 
# still important because the kernel uses those settings when starting [systemd]
# itself as the init process 1.

# $todo(jefflill):
#
# I need to think about whether this makes sense from a security perspective
# because this means that any malicious that manages to run (even as non-root) 
# will be able to max-out files and RAM and DOS other services.
#
# Now that we'll be installing a lot fewer Linux services after switching
# to Kubernetes, it probably makes more sense to set limits for the fewer
# specific services we're actually deploying.

cat <<EOF > /etc/security/limits.conf
# /etc/security/limits.conf
#
# Each line describes a limit for a user in the form:
#
#   <domain>        <type>  <item>  <value>
#
# Where:
#
#   <domain> can be:
#        - a user name
#        - a group name, with @group syntax
#        - the wildcard *, for default entry
#        - the wildcard %, can be also used with %group syntax,
# for maxlogin limit
#        - NOTE: group and wildcard limits are not applied to root.
# To apply a limit to the root user, <domain> must be
# the literal username root.
#
#   <type> can have the two values:
#        - ""soft"" for enforcing the soft limits
#        - ""hard"" for enforcing hard limits
#
#   <item> can be one of the following:
#        - core - limits the core file size (KB)
#        - data - max data size (KB)
#        - fsize - maximum filesize (KB)
#        - memlock - max locked-in-memory address space (KB)
#        - nofile - max number of open files
#        - rss - max resident set size (KB)
#        - stack - max stack size (KB)
#        - cpu - max CPU time (MIN)
#        - nproc - max number of processes
#        - as - address space limit (KB)
#        - maxlogins - max number of logins for this user
#        - maxsyslogins - max number of logins on the system
#        - priority - the priority to run user process with
#        - locks - max number of file locks the user can hold
#        - sigpending - max number of pending signals
#        - msgqueue - max memory used by POSIX message queues (bytes)
#        - nice - max nice priority allowed to raise to values: [-20, 19]
#        - rtprio - max realtime priority
#        - chroot - change root to directory (Debian-specific)
#
#<domain>   <type>  <item>  <value>

root    soft    nofile  unlimited
root    hard    nofile  unlimited
root    soft    memlock unlimited
root    hard    memlock unlimited
root    soft    nproc   unlimited
root    hard    nproc   unlimited

*       soft    nofile  unlimited
*       hard    nofile  unlimited
*       soft    memlock unlimited
*       hard    memlock unlimited
*       soft    nproc   unlimited
*       hard    nproc   unlimited

# End of file
EOF

#------------------------------------------------------------------------------
# [systemd] has its own configuration limits files and ignores
# [/etc/security/limits.conf] so we need to update the [systemd] settings 
# as well.

mkdir -p /etc/systemd/user.conf.d
chmod 764 /etc/systemd/user.conf.d

cat <<EOF > /etc/systemd/user.conf.d/50-neonkube.conf
# This file is part of systemd.
#
# systemd is free software; you can redistribute it and/or modify it
# under the terms of the GNU Lesser General Public License as published by
# the Free Software Foundation; either version 2.1 of the License, or
#  (at your option) any later version.
#
# You can override the directives in this file by creating files in
# /etc/systemd/user.conf.d/*.conf.
#
# See systemd-user.conf(5) for details

[Manager]
DefaultLimitNOFILE = infinity
DefaultLimitNPROC = infinity
DefaultLimitMEMLOCK = infinity
EOF

chmod 664 /etc/systemd/user.conf.d/50-neonkube.conf
cp /etc/systemd/user.conf.d/50-neonkube.conf /etc/systemd/system.conf

echo ""session required pam_limits.so"" >> /etc/pam.d/common-session

#------------------------------------------------------------------------------
# Tweak some kernel settings.  I extracted [/etc/sysctl.d/99-sysctl.conf] from
# a clean Ubuntu 22.04  install and then appended out changes after this line: 
# 
#   ##################################################
#   # neonKUBE tweaks
#
# Ubuntu 22.04 splits it's config files into seperate conf files in the 
# directlry and we're going to update the last file to be loaded:
# [/etc/sysctl.d/99-sysctl.conf].  Ubuntu 20.04 just had one big file.

cat <<EOF > /etc/sysctl.d/99-sysctl.conf
#
# /etc/sysctl.conf - Configuration file for setting system variables
# See /etc/sysctl.d/ for additional system variables.
# See sysctl.conf (5) for information.
#

# kernel.domainname = example.com

# Uncomment the following to stop low-level messages on console
# kernel.printk = 3 4 1 3

###################################################################
# Functions previously found in netbase
#

# Uncomment the next two lines to enable Spoof protection (reverse-path filter)
# Turn on Source Address Verification in all interfaces to
# prevent some spoofing attacks
# net.ipv4.conf.default.rp_filter=1
# net.ipv4.conf.all.rp_filter=1

# Uncomment the next line to enable TCP/IP SYN cookies
# See http://lwn.net/Articles/277146/
# Note: This may impact IPv6 TCP sessions too
# net.ipv4.tcp_syncookies=1

# Uncomment the next line to enable packet forwarding for IPv4
# net.ipv4.ip_forward=1

# Uncomment the next line to enable packet forwarding for IPv6
# Enabling this option disables Stateless Address Autoconfiguration
# based on Router Advertisements for this host
# net.ipv6.conf.all.forwarding=1


###################################################################
# Additional settings - these settings can improve the network
# security of the host and prevent against some network attacks
# including spoofing attacks and man in the middle attacks through
# redirection. Some network environments, however, require that these
# settings are disabled so review and enable them as needed.
#
# Do not accept ICMP redirects (prevent MITM attacks)
# net.ipv4.conf.all.accept_redirects = 0
# net.ipv6.conf.all.accept_redirects = 0
# _or_
# Accept ICMP redirects only for gateways listed in our default
# gateway list (enabled by default)
# net.ipv4.conf.all.secure_redirects = 1
#
# Do not send ICMP redirects (we are not a router)
# net.ipv4.conf.all.send_redirects = 0
#
# Do not accept IP source route packets (we are not a router)
# net.ipv4.conf.all.accept_source_route = 0
# net.ipv6.conf.all.accept_source_route = 0
#
# Log Martian Packets
# net.ipv4.conf.all.log_martians = 1
#

###################################################################
# Magic system request Key
# 0=disable, 1=enable all, >1 bitmask of sysrq functions
# See https://www.kernel.org/doc/html/latest/admin-guide/sysrq.html
# for what other values do
# kernel.sysrq=438

###################################################################
# TWEAK: neonKUBE settings:

# Explicitly set the maximum number of file descriptors for the
# entire system.  This looks like it defaults to [1048576] for
# Ubuntu 22.04 so we're going to pin this value to enforce
# consistency across Linux updates, etc.
fs.file-max = 4194303

# We'll allow processes to open the same number of file handles.
fs.nr_open = 1048576

# podman specific entries
fs.inotify.max_queued_events = 1048576
fs.inotify.max_user_instances = 1048576
fs.inotify.max_user_watches = 1048576

###################################################################
# Boost the number of RAM pages a process can map as well as increasing 
# the number of available source ephemeral TCP ports, pending connection
# backlog, packet receive queue size.

# Disable swap
vm.swappiness = 0

# Allow processes to lock up to 64GB worth of 4K pages into RAM.
vm.max_map_count = 16777216

# prioritize application RAM against disk/swap cache
vm.vfs_cache_pressure = 50

# minimum free memory
vm.min_free_kbytes = 67584

# increase the maximum length of processor input queues
net.core.netdev_max_backlog = 250000

# increase the TCP maximum and default buffer sizes using setsockopt()
net.core.rmem_max = 4194304
net.core.wmem_max = 4194304
net.core.rmem_default = 4194304
net.core.wmem_default = 4194304
net.core.optmem_max = 4194304

# maximum number of incoming connections
net.core.somaxconn = 65535

# Specify the range of TCP ports that can be used by client sockets.
net.ipv4.ip_local_port_range = 9000 65535

# queue length of completely established sockets waiting for accept
net.ipv4.tcp_max_syn_backlog = 4096

# disable the TCP timestamps option for better CPU utilization
net.ipv4.tcp_timestamps = 0

# MTU discovery, only enable when ICMP blackhole detected
net.ipv4.tcp_mtu_probing = 1

# time to wait (seconds) for FIN packet
net.ipv4.tcp_fin_timeout = 15

# enable low latency mode for TCP:
net.ipv4.tcp_low_latency = 1

# enable the TCP selective acks option for better throughput
net.ipv4.tcp_sack = 1

###################################################################
# Set the IPv4 and IPv6 packet TTL to 255 to try to ensure that packets
# will still make it to the destination in the face of perhaps a lot
# of hops added by clouds and Kubernetes (on both sides of the link).

net.ipv4.ip_default_ttl = 255
net.ipv6.conf.all.hop_limit = 255

# Kubernetes requires packet forwarding.
net.ipv4.ip_forward = 1

# CRI-O config.
net.bridge.bridge-nf-call-iptables  = 1
net.bridge.bridge-nf-call-ip6tables = 1

###################################################################
# Setting overrides recommended for custom Google Cloud images
#
# https://cloud.google.com/compute/docs/images/building-custom-os

# Enable syn flood protection
net.ipv4.tcp_syncookies = 1

# Ignore source-routed packets
net.ipv4.conf.all.accept_source_route = 0

# Ignore source-routed packets
net.ipv4.conf.default.accept_source_route = 0

# Ignore ICMP redirects
net.ipv4.conf.all.accept_redirects = 0

# Ignore ICMP redirects
net.ipv4.conf.default.accept_redirects = 0

# Ignore ICMP redirects from non-GW hosts
net.ipv4.conf.all.secure_redirects = 1

# Ignore ICMP redirects from non-GW hosts
net.ipv4.conf.default.secure_redirects = 1

# Don't allow traffic between networks or act as a router
# net.ipv4.ip_forward = 0

# Don't allow traffic between networks or act as a router
net.ipv4.conf.all.send_redirects = 0

# Don't allow traffic between networks or act as a router
net.ipv4.conf.default.send_redirects = 0

# Reverse path filtering - IP spoofing protection
net.ipv4.conf.all.rp_filter = 1

# Reverse path filtering - IP spoofing protection
net.ipv4.conf.default.rp_filter = 1

# Ignore ICMP broadcasts to avoid participating in Smurf attacks
net.ipv4.icmp_echo_ignore_broadcasts = 1

# Ignore bad ICMP errors
net.ipv4.icmp_ignore_bogus_error_responses = 1

# Log spoofed, source-routed, and redirect packets
net.ipv4.conf.all.log_martians = 1

# Log spoofed, source-routed, and redirect packets
net.ipv4.conf.default.log_martians = 1

# Implement RFC 1337 fix
net.ipv4.tcp_rfc1337 = 1

# set max number of connections
net.netfilter.nf_conntrack_max=1000000
net.nf_conntrack_max=1000000
net.netfilter.nf_conntrack_expect_max=1000
net.netfilter.nf_conntrack_buckets=250000

# Randomize addresses of mmap base, heap, stack and VDSO page
kernel.randomize_va_space = 2

# Provide protection from ToCToU races
fs.protected_hardlinks = 1

# Provide protection from ToCToU races
fs.protected_symlinks = 1

# Make locating kernel addresses more difficult
kernel.kptr_restrict = 1

# Set ptrace protections
kernel.yama.ptrace_scope = 1

# Set perf only available to root
kernel.perf_event_paranoid = 2
EOF

#------------------------------------------------------------------------------
# iptables may be configured to track only a small number of TCP connections by
# default.  We're going to explicitly set the limit to 1 million connections.
# This will consume about 8MiB of RAM (so not too bad).

# cat <<EOF > /etc/modprobe.d/nf_conntrack.conf
# Explicitly set the maximum number of TCP connections that iptables can track.
# Note that this number is multiplied by 8 to obtain the connection count.
# options nf_conntrack hashsize = 393216
# EOF

cat > /etc/modules <<EOF
ip_vs
ip_vs_rr
ip_vs_wrr
ip_vs_sh
nf_conntrack
EOF

#------------------------------------------------------------------------------
# Databases are generally not compatible with transparent huge pages.  It appears
# that the best way to disable this is with a simple service.

cat <<EOF > /lib/systemd/system/neon-disable-thp.service
# Disables transparent home pages.

[Unit]
Description=Disable transparent home pages (THP)

[Service]
Type=simple
ExecStart=/bin/sh -c ""echo 'never' > /sys/kernel/mm/transparent_hugepage/enabled && echo 'never' > /sys/kernel/mm/transparent_hugepage/defrag""

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neon-disable-thp
systemctl daemon-reload
systemctl restart neon-disable-thp

#------------------------------------------------------------------------------
# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].  This will allow us to easily capture these logs in The
# future so they can be included in a cluster logging solution.
#
# We're also setting [MaxRetentionSec=345600] which limits log local retention 
# to 4 days.  This overrides the default policy which will consume up to 10%
# of the local file system while still providing enough time for operators
# to manually review local logs when something bad happened to cluster logging.

cat <<EOF > /etc/systemd/journald.conf
#------------------------------------------------------------------------------
# FILE:         journald.conf
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].
#
# We're also setting [MaxRetentionSec=86400] which limits log local retention 
# to one day.  This overrides the default policy which will consume up to 10%
# of the local file system while still providing enough time for operators
# to manually review local logs when something bad happened to cluster logging.
# 
# See: https://www.freedesktop.org/software/systemd/man/journald.conf.html

[Journal]
Storage=persistent
# Compress=yes
# Seal=yes
# SplitMode=uid
# SyncIntervalSec=5m
# RateLimitInterval=30s
# RateLimitBurst=1000
# SystemMaxUse=
# SystemKeepFree=
# SystemMaxFileSize=
# SystemMaxFiles=100
# RuntimeMaxUse=
# RuntimeKeepFree=
# RuntimeMaxFileSize=
# RuntimeMaxFiles=100
MaxRetentionSec=345600
# MaxFileSec=1month
# ForwardToSyslog=yes
# ForwardToKMsg=no
# ForwardToConsole=no
# ForwardToWall=yes
# TTYPath=/dev/console
# MaxLevelStore=debug
# MaxLevelSyslog=debug
# MaxLevelKMsg=notice
# MaxLevelConsole=info
# MaxLevelWall=emerg
EOF

#------------------------------------------------------------------------------
# Install a simple service script that periodically cleans accumulated files
# on the cluster node.

# $todo(jefflill):
#
# The neon-cleaner assumes that nobody is going to have LinuxSshProxy 
# commands that run for more than one day (which is pretty unlikely).  A better approach
# would be to look for temporary command folders THAT HAVE COMPLETED (e.g. HAVE
# an [exit] code file) and are older than one day (or perhaps even older than an
# hour or two) and then purge just those.  Not a high priority.

cat <<EOF > {KubeNodeFolder.Bin}/neon-cleaner
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-cleaner
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

# This is a simple service script that periodically cleans accumulated files
# on the cluster node including:
#
#   1. Shred and delete the root account's [.bash-history] file 
# as a security measure.  These commands could include
# sensitive information such as credentials, etc.
#
#   2. Purge temporary Neon command files uploaded by LinuxSshProxy.  These
# are located within folder beneath [/dev/shm/neonkube/cmd].  Although
# LinuxSshProxy removes these files after commands finish executing, it
# is possible to see these accumulate if the session was interrupted.
# We'll purge folders and files older than one day.
#
$   3. Clean the temporary file LinuxSshProxy upload and execute folders.

history_path1=${{HOME}}/.bash_history
history_path2=/root/.bash_history
sleep_seconds=3600

echo ""[INFO] Starting: [sleep_time=\${{sleep_seconds}} seconds]""

while true
do
# Clean [.bash-history]

    if [ -f \${{history_path1}} ] ; then
        echo ""[INFO] Shredding [\${{history_path1}}]""
        result=\$(shred -uz \${{history_path1}})
        if [ ""\$?"" != ""0"" ] ; then
            echo ""[WARN] \${{result}}""
        fi
    fi

    if [ -f \${{history_path2}} ] ; then
        echo ""[INFO] Shredding [\${{history_path2}}]""
        result=\$(shred -uz \${{history_path2}})
        if [ ""\$?"" != ""0"" ] ; then
            echo ""[WARN] \${{result}}""
        fi
    fi

# Clean the [LinuxSshProxy] temporary download files.

    if [ -d ""$/home/sysadmin/.neon/download"" ] ; then
        echo ""[INFO] Cleaning: /home/sysadmin/.neon/download""
        find ""/home/sysadmin/.neon/download/*"" -type d -ctime +1 | xargs rm -rf
    fi

# Clean the [LinuxSshProxy] temporary exec files.

    if [ -d ""/home/sysadmin/.neon/exec"" ] ; then
        echo ""[INFO] Cleaning: ""/home/sysadmin/.neon/exec""""
        find ""/home/sysadmin/.neonklube/exec/*"" -type d -ctime +1 | xargs rm -rf
    fi

    if [ -d ""/home/sysadmin/.neon/upload"" ] ; then
        echo ""[INFO] Cleaning: /home/sysadmin/.neon/upload""
        find ""/home/sysadmin/.neon/upload/*"" -type d -ctime +1 | xargs rm -rf
    fi

# Sleep for a while before trying again.

    sleep \${{sleep_seconds}}
done
EOF

chmod 700 {KubeNodeFolder.Bin}/neon-cleaner

# Generate the [neon-cleaner] systemd unit.

cat <<EOF > /lib/systemd/system/neon-cleaner.service
# A service that periodically shreds the root's Bash history
# as a security measure.

[Unit]
Description=neon-cleaner
Documentation=
After=local-fs.target
Requires=local-fs.target

[Service]
ExecStart={KubeNodeFolder.Bin}/neon-cleaner
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

# Enable the new services.

systemctl enable neon-cleaner
systemctl enable iscsid
systemctl daemon-reload
";
            InvokeIdempotent("base/initialize",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "node (low-level)");

                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Disables the <b>neon-init</b> service during cluster setup because it is no
        /// longer necessary after the node first boots and its credentials and network
        /// settings have been configured.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeDisableNeonInit(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            InvokeIdempotent("base/disable-neon-init",
                () =>
                {
                    controller.LogProgress(this, verb: "disable", message: "neon-init.service");

                    SudoCommand("systemctl disable neon-init.service", RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the necessary packages and configures setup for <b>IPVS</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallIPVS(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("setup/ipvs",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "ipvs");

                    var setupScript =
$@"
set -euo pipefail

{KubeNodeFolder.Bin}/safe-apt-get update -y
{KubeNodeFolder.Bin}/safe-apt-get install -y ipset ipvsadm
";
                    SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>CRI-O</b> container runtime.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="clusterManifest">The cluster manifest.</param>
        public void NodeInstallCriO(ISetupController controller, ClusterManifest clusterManifest)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(clusterManifest != null, nameof(clusterManifest));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var reconfigureOnly = !controller.Get<bool>(KubeSetupProperty.Preparing, true);

            controller.LogProgress(this, verb: "setup", message: "cri-o");

            // $note(jefflill):
            //
            // We used to perform the configuration operation below as an
            // idempotent operation but we need to relax that so that we
            // can apply configuration changes on new nodes created from
            // the node image.

            if (!reconfigureOnly)
            {
                var moduleScript =
@"
set -euo pipefail

# Create the .conf file to load required modules during boot.

cat <<EOF > /etc/modules-load.d/crio.conf
overlay
br_netfilter
EOF

# ...and load them explicitly now.

modprobe overlay
modprobe br_netfilter

sysctl --quiet --load /etc/sysctl.conf
";          
                SudoCommand(CommandBundle.FromScript(moduleScript));
            }

            //-----------------------------------------------------------------
            // Generate the container registry config file contents for:
            //
            //      /etc/containers/registries.conf.d/00-neon-cluster.conf
            //
            // NOTE: We're only generating the reference to the built-in local
            //       Harbor registry here.  Any additional registries will be
            //       configured during cluster setup as custom [ContainerRegistry]
            //       resources to be configured by the [neon-node-agent].
            //
            // We'll add this to the installation script below.

            // $hack(jefflill):
            //
            // [cluster] will be NULL when preparing a node image so we'll set the
            // default here and this will be reconfigured during cluster setup.

            var sbRegistryConfig = new StringBuilder();

            sbRegistryConfig.Append(
$@"
unqualified-search-registries = []

[[registry]]
prefix   = ""{KubeConst.LocalClusterRegistry}""
insecure = true
blocked  = false
location = ""{KubeConst.LocalClusterRegistry}""
");

            //-----------------------------------------------------------------
            // Install and configure CRI-O.

            // $note(jefflill):
            //
            // Version pinning doesn't seem to work:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1563

            var crioVersionFull    = Version.Parse(KubeVersions.Crio);
            var crioVersionNoPatch = new Version(crioVersionFull.Major, crioVersionFull.Minor);
            var crioVersionPinned  = $"{crioVersionNoPatch}:{crioVersionFull}";
            var install            = reconfigureOnly ? "false" : "true";

            var setupScript =
$@"
if [ ""{install}"" = ""true"" ]; then

    set -euo pipefail

# Initialize the OS and VERSION environment variables.

    OS=xUbuntu_22.04
    VERSION={crioVersionNoPatch}

# Install the CRI-O packages.

    echo ""deb [signed-by=/usr/share/keyrings/libcontainers-archive-keyring.gpg] https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/$OS/ /"" > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
    echo ""deb [signed-by=/usr/share/keyrings/libcontainers-crio-archive-keyring.gpg] https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/$VERSION/$OS/ /"" > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable:cri-o:$VERSION.list

    mkdir -p /usr/share/keyrings

    curl -L https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/$OS/Release.key > /tmp/key.txt
    cat /tmp/key.txt | gpg --dearmor --yes -o /usr/share/keyrings/libcontainers-archive-keyring.gpg
    rm /tmp/key.txt

    curl -L https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/$VERSION/$OS/Release.key > /tmp/key.txt
    cat /tmp/key.txt | gpg --dearmor --yes -o /usr/share/keyrings/libcontainers-crio-archive-keyring.gpg
    rm /tmp/key.txt

    {KubeNodeFolder.Bin}/safe-apt-get update -y
    {KubeNodeFolder.Bin}/safe-apt-get install -y cri-o cri-o-runc
fi

set -euo pipefail

# Generate the CRI-O configuration.

cat <<EOF > /etc/containers/registries.conf.d/00-neon-cluster.conf
{sbRegistryConfig}
EOF

cat <<EOF > /etc/crio/crio.conf
# The CRI-O configuration file specifies all of the available configuration
# options and command-line flags for the crio(8) OCI Kubernetes Container Runtime
# daemon, but in a TOML format that can be more easily modified and versioned.
#
# Please refer to crio.conf(5) for details of all configuration options.

# CRI-O supports partial configuration reload during runtime, which can be
# done by sending SIGHUP to the running process. Currently supported options
# are explicitly mentioned with: 'This option supports live configuration
# reload'.

# CRI-O reads its storage defaults from the containers-storage.conf(5) file
# located at /etc/containers/storage.conf. Modify this storage configuration if
# you want to change the system's defaults. If you want to modify storage just
# for CRI-O, you can change the storage configuration options here.
[crio]

# Path to the ""root directory"". CRI-O stores all of its data, including
# containers images, in this directory.
# root = ""/var/lib/containers/storage""

# Path to the ""run directory"". CRI-O stores all of its state in this directory.
# runroot = ""/var/run/containers/storage""

# Storage driver used to manage the storage of images and containers. Please
# refer to containers-storage.conf(5) to see all available storage drivers.
# storage_driver = """"

# List to pass options to the storage driver. Please refer to
# containers-storage.conf(5) to see all available storage options.
#storage_option = [
#]

# The default log directory where all logs will go unless directly specified by
# the kubelet. The log directory specified must be an absolute directory.
log_dir = ""/var/log/crio/pods""

# Location for CRI-O to lay down the temporary version file.
# It is used to check if crio wipe should wipe containers, which should
# always happen on a node reboot
version_file = ""/var/run/crio/version""

# Location for CRI-O to lay down the persistent version file.
# It is used to check if crio wipe should wipe images, which should
# only happen when CRI-O has been upgraded
version_file_persist = ""/var/lib/crio/version""

# The crio.api table contains settings for the kubelet/gRPC interface.
[crio.api]

# Path to AF_LOCAL socket on which CRI-O will listen.
listen = ""/var/run/crio/crio.sock""

# IP address on which the stream server will listen.
stream_address = ""127.0.0.1""

# The port on which the stream server will listen. If the port is set to ""0"", then
# CRI-O will allocate a random free port number.
stream_port = ""0""

# Enable encrypted TLS transport of the stream server.
stream_enable_tls = false

# Path to the x509 certificate file used to serve the encrypted stream. This
# file can change, and CRI-O will automatically pick up the changes within 5
# minutes.
stream_tls_cert = """"

# Path to the key file used to serve the encrypted stream. This file can
# change and CRI-O will automatically pick up the changes within 5 minutes.
stream_tls_key = """"

# Path to the x509 CA(s) file used to verify and authenticate client
# communication with the encrypted stream. This file can change and CRI-O will
# automatically pick up the changes within 5 minutes.
stream_tls_ca = """"

# Maximum grpc send message size in bytes. If not set or <=0, then CRI-O will default to 16 * 1024 * 1024.
grpc_max_send_msg_size = 16777216

# Maximum grpc receive message size. If not set or <= 0, then CRI-O will default to 16 * 1024 * 1024.
grpc_max_recv_msg_size = 16777216

# The crio.runtime table contains settings pertaining to the OCI runtime used
# and options for how to set up and manage the OCI runtime.
[crio.runtime]

# A list of ulimits to be set in containers by default, specified as
# ""<ulimit name>=<soft limit>:<hard limit>"", for example:
# ""nofile=1024:2048""
# If nothing is set here, settings will be inherited from the CRI-O daemon
# default_ulimits = [
#]

# default_runtime is the _name_ of the OCI runtime to be used as the default.
# The name is matched against the runtimes map below.
default_runtime = ""runc""

# If true, the runtime will not use pivot_root, but instead use MS_MOVE.
no_pivot = false

# decryption_keys_path is the path where the keys required for
# image decryption are stored. This option supports live configuration reload.
decryption_keys_path = ""/etc/crio/keys/""

# Path to the conmon binary, used for monitoring the OCI runtime.
# Will be searched for using $PATH if empty.
conmon = """"

# Cgroup setting for conmon
conmon_cgroup = ""system.slice""

# Environment variable list for the conmon process, used for passing necessary
# environment variables to conmon or the runtime.
conmon_env = [
        ""PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"",
]

# Additional environment variables to set for all the
# containers. These are overridden if set in the
# container image spec or in the container runtime configuration.
default_env = [
]

# If true, SELinux will be used for pod separation on the host.
selinux = false

# Path to the seccomp.json profile which is used as the default seccomp profile
# for the runtime. If not specified, then the internal default seccomp profile
# will be used. This option supports live configuration reload.
seccomp_profile = """"

# Used to change the name of the default AppArmor profile of CRI-O. The default
# profile name is ""crio-default"". This profile only takes effect if the user
# does not specify a profile via the Kubernetes Pod's metadata annotation. If
# the profile is set to ""unconfined"", then this equals to disabling AppArmor.
# This option supports live configuration reload.
apparmor_profile = ""crio-default""

# Cgroup management implementation used for the runtime.
cgroup_manager = ""systemd""

# List of default capabilities for containers. If it is empty or commented out,
# only the capabilities defined in the containers json file by the user/kube
# will be added.
default_capabilities = [
        ""CHOWN"",
        ""DAC_OVERRIDE"",
        ""FSETID"",
        ""FOWNER"",
        ""SETGID"",
        ""SETUID"",
        ""SETPCAP"",
        ""NET_BIND_SERVICE"",
        ""KILL"",
]

# List of default sysctls. If it is empty or commented out, only the sysctls
# defined in the container json file by the user/kube will be added.
default_sysctls = [
]

# List of additional devices. specified as
# ""<device-on-host>:<device-on-container>:<permissions>"", for example: ""--device=/dev/sdc:/dev/xvdc:rwm"".
# If it is empty or commented out, only the devices
# defined in the container json file by the user/kube will be added.
additional_devices = [
]

# Path to OCI hooks directories for automatically executed hooks. If one of the
# directories does not exist, then CRI-O will automatically skip them.
hooks_dir = [
        ""/usr/share/containers/oci/hooks.d"",
]

# List of default mounts for each container. **Deprecated:** this option will
# be removed in future versions in favor of default_mounts_file.
default_mounts = [
]

# Path to the file specifying the defaults mounts for each container. The
# format of the config is /SRC:/DST, one mount per line. Notice that CRI-O reads
# its default mounts from the following two files:
#
#   1) /etc/containers/mounts.conf (i.e., default_mounts_file): This is the
# override file, where users can either add in their own default mounts, or
# override the default mounts shipped with the package.
#
#   2) /usr/share/containers/mounts.conf: This is the default file read for
# mounts. If you want CRI-O to read from a different, specific mounts file,
# you can change the default_mounts_file. Note, if this is done, CRI-O will
# only add mounts it finds in this file.
#
# default_mounts_file = """"

# Maximum number of processes allowed in a container.
pids_limit = 1024

# Maximum sized allowed for the container log file. Negative numbers indicate
# that no size limit is imposed. If it is positive, it must be >= 8192 to
# match/exceed conmon's read buffer. The file is truncated and re-opened so the
# limit is never exceeded.
log_size_max = -1

# Whether container output should be logged to journald in addition to the kuberentes log file
log_to_journald = false

# Path to directory in which container exit files are written to by conmon.
container_exits_dir = ""/var/run/crio/exits""

# Path to directory for container attach sockets.
container_attach_socket_dir = ""/var/run/crio""

# The prefix to use for the source of the bind mounts.
bind_mount_prefix = """"

# If set to true, all containers will run in read-only mode.
read_only = false

# Changes the verbosity of the logs based on the level it is set to. Options
# are fatal, panic, error, warn, info, debug and trace. This option supports
# live configuration reload.
log_level = ""info""

# Filter the log messages by the provided regular expression.
# This option supports live configuration reload.
log_filter = """"

# The UID mappings for the user namespace of each container. A range is
# specified in the form containerUID:HostUID:Size. Multiple ranges must be
# separated by comma.
uid_mappings = """"

# The GID mappings for the user namespace of each container. A range is
# specified in the form containerGID:HostGID:Size. Multiple ranges must be
# separated by comma.
gid_mappings = """"

# The minimal amount of time in seconds to wait before issuing a timeout
# regarding the proper termination of the container. The lowest possible
# value is 30s, whereas lower values are not considered by CRI-O.
ctr_stop_timeout = 30

# **DEPRECATED** this option is being replaced by manage_ns_lifecycle, which is described below.
# manage_network_ns_lifecycle = false

# manage_ns_lifecycle determines whether we pin and remove namespaces
# and manage their lifecycle
manage_ns_lifecycle = false

# The directory where the state of the managed namespaces gets tracked.
# Only used when manage_ns_lifecycle is true.
namespaces_dir = ""/var/run""

# pinns_path is the path to find the pinns binary, which is needed to manage namespace lifecycle
pinns_path = """"

# The ""crio.runtime.runtimes"" table defines a list of OCI compatible runtimes.
# The runtime to use is picked based on the runtime_handler provided by the CRI.
# If no runtime_handler is provided, the runtime will be picked based on the level
# of trust of the workload. Each entry in the table should follow the format:
#
#[crio.runtime.runtimes.runtime-handler]
# runtime_path = ""/path/to/the/executable""
# runtime_type = ""oci""
# runtime_root = ""/path/to/the/root""
#
# Where:
# - runtime-handler: name used to identify the runtime
# - runtime_path (optional, string): absolute path to the runtime executable in
# the host filesystem. If omitted, the runtime-handler identifier should match
# the runtime executable name, and the runtime executable should be placed
# in $PATH.
# - runtime_type (optional, string): type of runtime, one of: ""oci"", ""vm"". If
# omitted, an ""oci"" runtime is assumed.
# - runtime_root (optional, string): root directory for storage of containers
# state.

[crio.runtime.runtimes.runc]
runtime_path = """"
runtime_type = ""oci""
runtime_root = ""/run/runc""

# Kata Containers is an OCI runtime, where containers are run inside lightweight
# VMs. Kata provides additional isolation towards the host, minimizing the host attack
# surface and mitigating the consequences of containers breakout.

# Kata Containers with the default configured VMM
#[crio.runtime.runtimes.kata-runtime]

# Kata Containers with the QEMU VMM
#[crio.runtime.runtimes.kata-qemu]

# Kata Containers with the Firecracker VMM
#[crio.runtime.runtimes.kata-fc]

# The crio.image table contains settings pertaining to the management of OCI images.
#
# CRI-O reads its configured registries defaults from the system wide
# containers-registries.conf(5) located in /etc/containers/registries.conf. If
# you want to modify just CRI-O, you can change the registries configuration in
# this file. Otherwise, leave insecure_registries and registries commented out to
# use the system's defaults from /etc/containers/registries.conf.
[crio.image]

# Default transport for pulling images from a remote container storage.
default_transport = ""docker://""

# The path to a file containing credentials necessary for pulling images from
# secure registries. The file is similar to that of /var/lib/kubelet/config.json
global_auth_file = """"

# The image used to instantiate infra containers.
# This option supports live configuration reload.
pause_image = ""{KubeConst.LocalClusterRegistry}/pause:{KubeVersions.Pause}""

# The path to a file containing credentials specific for pulling the pause_image from
# above. The file is similar to that of /var/lib/kubelet/config.json
# This option supports live configuration reload.
pause_image_auth_file = """"

# The command to run to have a container stay in the paused state.
# When explicitly set to """", it will fallback to the entrypoint and command
# specified in the pause image. When commented out, it will fallback to the
# default: ""/pause"". This option supports live configuration reload.
pause_command = ""/pause""

# Path to the file which decides what sort of policy we use when deciding
# whether or not to trust an image that we've pulled. It is not recommended that
# this option be used, as the default behavior of using the system-wide default
# policy (i.e., /etc/containers/policy.json) is most often preferred. Please
# refer to containers-policy.json(5) for more details.
signature_policy = """"

# List of registries to skip TLS verification for pulling images. Please
# consider configuring the registries via /etc/containers/registries.conf before
# changing them here.
# insecure_registries = ""[]""

# Controls how image volumes are handled. The valid values are mkdir, bind and
# ignore; the latter will ignore volumes entirely.
image_volumes = ""mkdir""

# List of registries to be used when pulling an unqualified image (e.g.,
# ""alpine:latest""). By default, registries is set to ""docker.io"" for
# compatibility reasons. Depending on your workload and usecase you may add more
# registries (e.g., ""quay.io"", ""registry.fedoraproject.org"",
# ""registry.opensuse.org"", etc.).
# registries = []

# The crio.network table containers settings pertaining to the management of
# CNI plugins.
[crio.network]

# The default CNI network name to be selected. If not set or """", then
# CRI-O will pick-up the first one found in network_dir.
# cni_default_network = """"

# Path to the directory where CNI configuration files are located.
network_dir = ""/etc/cni/net.d/""

# Paths to directories where CNI plugin binaries are located.
plugin_dirs = [""/opt/cni/bin/""]

# A necessary configuration for Prometheus based metrics retrieval
[crio.metrics]

# Globally enable or disable metrics support.
enable_metrics = true

# The port on which the metrics server will listen.
metrics_port = 9090
EOF

cat <<EOF > /etc/cni/net.d/100-crio-bridge.conf
{{
    ""cniVersion"": ""0.3.1"",
    ""name"": ""crio"",
    ""type"": ""bridge"",
    ""bridge"": ""cni0"",
    ""isGateway"": true,
    ""ipMasq"": true,
    ""hairpinMode"": true,
    ""ipam"": {{
        ""type"": ""host-local"",
        ""routes"": [
            {{ ""dst"": ""0.0.0.0/0"" }}
        ],
        ""ranges"": [
            [{{ ""subnet"": ""10.85.0.0/16"" }}]
        ]
    }}
}}
EOF

# Remove shortnames.

rm -f /etc/containers/registries.conf.d/000-shortnames.conf

# Configure CRI-O to start on boot and then restart it to pick up the new options.

systemctl daemon-reload
systemctl enable crio
systemctl restart crio

# Prevent the package manager from automatically upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold cri-o cri-o-runc
";
            SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);

            // $todo(jefflill): Remove this hack when/if it's no longer necessary.
            //
            // We need to install a slightly customized version of CRI-O that has
            // a somewhat bastardized implementation of container image "pinning",
            // to prevent Kubelet from evicting critical setup images that cannot
            // be pulled automatically from GHCR.
            //
            // The customized version is built by [neon-image build cri-o ...]
            // and [neon-image prepare node ...] (by default) with the binary
            // being uploaded to S3.
            //
            // The custom CRI-O loads images references from [/etc/neonkube/pinned-images]
            // (one per line) and simply prevents any listed images from being deleted.
            // As of CRI API 1.23+, the list-images endpoint can return a pinned field
            // which Kubelet will honor when handling disk preassure, but unfortunately
            // CRI-O as of 1.23.0 doesn't appear to have vendored the updated CRI yet.
            // It does look like pinning is coming soon, so we can probably stick with
            // this delete based approach until then.

            if (!reconfigureOnly)
            {
                var crioUpdateScript =
$@"
set -euo pipefail

# Install the [pinned-images] config file.

cp pinned-images /etc/neonkube/pinned-images
chmod 664 /etc/neonkube/pinned-images

# Replace the CRI-O binary with our custom one.

systemctl stop crio
curl {KubeHelper.CurlOptions} {KubeDownloads.NeonPublicBucketUri}/cri-o/crio.{KubeVersions.Crio}.gz | gunzip --stdout > /usr/bin/crio
systemctl start crio
";
                var bundle         = CommandBundle.FromScript(crioUpdateScript);
                var sbPinnedImages = new StringBuilder();

                // Generate the [/etc/neonkube/pinned-images] config file listing the 
                // the container images that our modified version of CRI-O will consider
                // as pinned and will not allow these images to be removed by Kubelet
                // and probably via podman as well.

                sbPinnedImages.AppendLine("# WARNING: automatic generation");
                sbPinnedImages.AppendLine();
                sbPinnedImages.AppendLine("# This file is generated automatically during cluster setup as well as by the");
                sbPinnedImages.AppendLine("# [neon-node-agent] so any manual changes may be overwitten at any time.");
                sbPinnedImages.AppendLine();
                sbPinnedImages.AppendLine("# This file specifies the container image references to be treated as pinned");
                sbPinnedImages.AppendLine("# by a slightly customized version of CRI-O which blocks the removal of any");
                sbPinnedImages.AppendLine("# image references listed here.");

                foreach (var image in clusterManifest.ContainerImages)
                {
                    sbPinnedImages.AppendLine();
                    sbPinnedImages.AppendLine(image.SourceRef);
                    sbPinnedImages.AppendLine(image.SourceDigestRef);
                    sbPinnedImages.AppendLine(image.InternalRef);
                    sbPinnedImages.AppendLine(image.InternalDigestRef);

                    // We need to extract the image ID from the internal digest reference which
                    // look something like this:
                    //
                    //      registry.neon.local/redis@sha256:561AABD123...
                    //
                    // where we need to extract the HEX bytes after the [@sha256:] prefix

                    var idPrefix    = "@sha256:";
                    var idPrefixPos = image.InternalDigestRef.IndexOf(idPrefix);

                    Covenant.Assert(idPrefixPos != -1);

                    sbPinnedImages.AppendLine(image.InternalDigestRef.Substring(idPrefixPos + idPrefix.Length));
                }

                bundle.AddFile("pinned-images", sbPinnedImages.ToString());

                SudoCommand(bundle).EnsureSuccess();
            }
        }

        /// <summary>
        /// Installs the <b>podman</b> CLI for managing <b>CRI-O</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallPodman(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/podman",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "podman");

                    var setupScript =
$@"
# $todo(jefflill):
#
# [podman] installation is having some trouble with a couple package depedencies,
# which causes the install command to return a non-zero exit code.  This appears
# to be a problem with two package dependencies trying to update the same file.
#
# We're going to use an option to force the file overwrite and then ignore any
# errors for now and hope for the best.  This isn't as bad as it sounds because 
# for neonKUBE we're only calling this method while creating node images, so we'll
# should be well aware of any problems while completing the node image configuration
# and then deploying test clusters.
#
#       https://github.com/nforgeio/neonKUBE/issues/1571
#       https://github.com/containers/podman/issues/14367
#
# I'm going to hack this for now by using this option:
#
#   -o Dpkg::Options::=""--force-overwrite""
#
# and ignoring errors from the command.

set -euo pipefail

{KubeNodeFolder.Bin}/safe-apt-get update -q
set +e                                                                                          # <--- HACK: disable error checks
{KubeNodeFolder.Bin}/safe-apt-get install -yq podman -o Dpkg::Options::=""--force-overwrite""   # <--- HACK: ignore overwrite errors
ln -s /usr/bin/podman /bin/docker

# Prevent the package manager from automatically upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold podman
";
                    SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>Helm</b> client.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallHelm(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/helm-client",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "helm client");

                    var script =
$@"
set -euo pipefail

cd /tmp
curl {KubeHelper.CurlOptions} {KubeDownloads.HelmLinuxUri} > helm.tar.gz
tar xvf helm.tar.gz
cp linux-amd64/helm /usr/local/bin
chmod 770 /usr/local/bin/helm
rm -f helm.tar.gz
rm -rf linux-amd64
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the <b>Kustomize</b> client.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallKustomize(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/kustomize-client",
                () =>
                {
                    controller.LogProgress(this, verb: "setup", message: "kustomize");

                    var script =
$@"
set -euo pipefail

cd /usr/local/bin
curl {KubeHelper.CurlOptions} https://raw.githubusercontent.com/kubernetes-sigs/kustomize/master/hack/install_kustomize.sh  > install-kustomize.sh

bash ./install-kustomize.sh {KubeVersions.Kustomize}
chmod 770 kustomize
rm  install-kustomize.sh
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Loads the docker images onto the node. This is used for debug mode only.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="downloadParallel">The optional limit for parallelism when downloading images from GitHub registry.</param>
        /// <param name="loadParallel">The optional limit for parallelism when loading images into the cluster.</param>
        public async Task NodeLoadImagesAsync(
            ISetupController    controller, 
            int                 downloadParallel = 5, 
            int                 loadParallel     = 2)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            await InvokeIdempotentAsync("setup/debug-load-images",
                async () =>
                {
                    if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")))
                    {
                        await Task.CompletedTask;
                        return;
                    }

                    controller.LogProgress(this, verb: "load", message: "container images (debug mode)");

                    var dockerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $@"DockerDesktop\version-bin\docker.exe");

                    var images = new List<NodeImageInfo>();

                    var setupImageFolders = Directory.EnumerateDirectories($"{Environment.GetEnvironmentVariable("NC_ROOT")}/Images/setup")
                        .Select(path => Path.GetFullPath(path))
                        .ToArray();

                    var registry = controller.Get<bool>(KubeSetupProperty.ReleaseMode) ? "ghcr.io/neonkube" : "ghcr.io/neonkube-dev";

                    var pullImageTasks = new List<Task>();

                    foreach (var imageFolder in setupImageFolders)
                    {
                        var imageName   = Path.GetFileName(imageFolder);
                        var versionPath = Path.Combine(imageFolder, ".version");
                        var importPath  = Path.Combine(imageFolder, ".import");
                        var targetTag   = (string)null;

                        if (File.Exists(versionPath))
                        {
                            targetTag = File.ReadAllLines(versionPath).First().Trim();

                            if (string.IsNullOrEmpty(targetTag))
                            {
                                throw new FileNotFoundException($"Setup image folder [{imageFolder}] has an empty a [.version] file.");
                            }
                        }
                        else
                        {
                            Covenant.Assert(File.Exists(importPath));

                            targetTag = $"neonkube-{KubeVersions.NeonKube}";
                        }

                        var sourceImage = $"{registry}/{imageName}:{targetTag}";

                        while (pullImageTasks.Where(task => task.Status < TaskStatus.RanToCompletion).Count() >= downloadParallel)
                        {
                            await Task.Delay(100);
                        }

                        images.Add(new NodeImageInfo(imageFolder, imageName, targetTag, registry));
                        pullImageTasks.Add(NeonHelper.ExecuteCaptureAsync(dockerPath, new string[] { "pull", sourceImage }));
                    }

                    await NeonHelper.WaitAllAsync(pullImageTasks);

                    var loadImageTasks = new List<Task>();

                    foreach (var image in images)
                    {
                        while (loadImageTasks.Where(task => task.Status < TaskStatus.RanToCompletion).Count() >= loadParallel)
                        {
                            await Task.Delay(100);
                        }

                        loadImageTasks.Add(LoadImageAsync(image));
                    }

                    await NeonHelper.WaitAllAsync(loadImageTasks);

                    SudoCommand($"rm -rf /tmp/container-image-*");
                });
        }

        /// <summary>
        /// Method to load specific container image onto the the node.
        /// </summary>
        /// <param name="image">The image.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task LoadImageAsync(NodeImageInfo image)
        {
            await SyncContext.Clear;
            await Task.Yield();

            var dockerPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), $@"DockerDesktop\version-bin\docker.exe");

            await InvokeIdempotentAsync($"setup/debug-load-images-{image.Name}",
                async () =>
                {
                    await Task.CompletedTask;

                    var id = Guid.NewGuid().ToString("d");

                    using (var tempFile = new TempFile(suffix: ".image.tar", null))
                    {
                        NeonHelper.ExecuteCapture(dockerPath,
                            new string[] {
                                "save",
                                "--output", tempFile.Path,
                                image.SourceImage }).EnsureSuccess();

                        using (var imageStream = new FileStream(tempFile.Path, FileMode.Open, FileAccess.ReadWrite))
                        {
                            Upload($"/tmp/container-image-{id}.tar", imageStream);

                            SudoCommand("podman load", RunOptions.Defaults | RunOptions.FaultOnError,
                                new string[]
                                {
                                    "--input", $"/tmp/container-image-{id}.tar"
                                });

                            SudoCommand("podman tag", RunOptions.Defaults | RunOptions.FaultOnError,
                                new string[]
                                {
                                    image.SourceImage, image.TargetImage
                                });

                            SudoCommand($"rm /tmp/container-image-{id}.tar");
                        }
                    }
                });
        }
  
        /// <summary>
        /// Installs the Kubernetes components: <b>kubeadm</b>, <b>kubectl</b>, and <b>kublet</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallKubernetes(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/kubernetes",
                () =>
                {
                    var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

                    // Perform the install.

                    var mainScript =
$@"
# $todo(jefflill):
#
# [kubeadm] installation is having some trouble with a couple package depedencies,
# which causes the install command to return a non-zero exit code.  This appears
# to be a problem with two package dependencies trying to update the same file.
#
# We're going to use an option to force the file overwrite and then ignore any
# errors for now and hope for the best.  This isn't as bad as it sounds because 
# for neonKUBE we're only calling this method while creating node images, so we'll
# should be well aware of any problems while completing the node image configuration
# and then deploying test clusters.
#
#       https://github.com/nforgeio/neonKUBE/issues/1571
#       https://github.com/containers/podman/issues/14367
#
# I'm going to hack this for now by using this option:
#
#   -o Dpkg::Options::=""--force-overwrite""
#
# and ignoring errors from the command.

set -euo pipefail

curl {KubeHelper.CurlOptions} https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
echo ""deb https://apt.kubernetes.io/ kubernetes-xenial main"" > /etc/apt/sources.list.d/kubernetes.list
{KubeNodeFolder.Bin}/safe-apt-get update

set +e                                                                                                                                              # <--- HACK: disable error checks
{KubeNodeFolder.Bin}/safe-apt-get install -yq kubeadm={KubeVersions.KubeAdminPackage} -o Dpkg::Options::=""--force-overwrite""

# Note that the [kubeadm] install also installs [kubelet] and [kubectl] but that the
# versions installed may be more recent than the Kubernetes version.  We want our
# clusters to use consistent versions of all tools so we're going to install these
# two packages again with specific versions and allow them to be downgraded.

{KubeNodeFolder.Bin}/safe-apt-get install -yq --allow-downgrades kubelet={KubeVersions.KubeletPackage} -o Dpkg::Options::=""--force-overwrite""     # <--- HACK: ignore overwrite errors
{KubeNodeFolder.Bin}/safe-apt-get install -yq --allow-downgrades kubectl={KubeVersions.KubectlPackage} -o Dpkg::Options::=""--force-overwrite""     # <--- HACK: ignore overwrite errors

# Prevent the package manager these components from starting automatically.

set +e      # Don't exit if the next command fails
apt-mark hold kubeadm kubectl kubelet
set -euo pipefail

# Pull the core Kubernetes container images (kube-scheduler, kube-proxy,...) to ensure they'll 
# be present on all node images.

kubeadm config images pull

# Configure kublet:

mkdir -p /opt/cni/bin
mkdir -p /etc/cni/net.d

echo KUBELET_EXTRA_ARGS=--container-runtime-endpoint='unix:///var/run/crio/crio.sock' > /etc/default/kubelet

# Stop and disable [kubelet] for now.  We'll enable this later during cluster setup.

systemctl daemon-reload
systemctl stop kubelet
systemctl disable kubelet
";
                    controller.LogProgress(this, verb: "setup", message: "kubernetes");

                    SudoCommand(CommandBundle.FromScript(mainScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }
    }
}
