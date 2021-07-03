//-----------------------------------------------------------------------------
// FILE:	    NodeSshProxy.NodePrepare.cs
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
        /// Installs the neonKUBE related tools to the <see cref="KubeNodeFolders.Bin"/> folder.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallTools(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/tools",
                () =>
                {
                    foreach (var file in KubeHelper.Resources.GetDirectory("/Tools").GetFiles())
                    {
                        controller.LogProgress(this, verb: "deploy", message: "tools");

                        // Upload each tool script, removing the extension.

                        var targetName = LinuxPath.GetFileNameWithoutExtension(file.Path);
                        var targetPath = LinuxPath.Combine(KubeNodeFolders.Bin, targetName);

                        UploadText(targetPath, file.ReadAllText(), permissions: "774", owner: KubeConst.SysAdminUser);
                    }
                });
        }

        /// <summary>
        /// Configures a node's host public SSH key during node provisioning.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void ConfigureSshKey(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            // Configure the SSH credentials on the node.

            InvokeIdempotent("base/ssh",
                () =>
                {
                    CommandBundle bundle;

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

            controller.LogProgress(this, verb: "verify", message: "ssh keys");

            Disconnect();
            UpdateCredentials(SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, clusterLogin.SshKey.PrivatePEM));
            WaitForBoot();

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
# neonKUBE: 
#
# Filter [rsyslog.service] log events we don't care about.

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

                    NodeInstallTools(controller);
                    BaseConfigureApt(controller, clusterDefinition.NodeOptions.PackageManagerRetries, clusterDefinition.NodeOptions.AllowPackageManagerIPv6);
                    BaseConfigureOpenSsh(controller);
                    DisableSnap(controller);
                    ConfigureJournald(controller);
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

            if (hostEnvironment != HostingEnvironment.Wsl2)
            {
                InvokeIdempotent("base/blacklist-floppy",
                    () =>
                    {
                        controller.LogProgress(this, verb: "blacklist", message: "floppy drive");

                        var floppyScript =
@"
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
# Enable system statistics collection (e.g. Page Faults,...)

sed -i '/^ENABLED=""false""/c\ENABLED=""true""' /etc/default/sysstat
";
                        SudoCommand(CommandBundle.FromScript(statScript));
                    });
            }

            var script =
$@"
#------------------------------------------------------------------------------
# Basic initialization

timedatectl set-timezone UTC

#------------------------------------------------------------------------------
# We need to increase the number of file descriptors and also how much memory
# can be locked by root processes.  We're simply going to overwrite the default
# version of [/etc/security/limits.conf] with our own copy.
#
# Note that [systemd] ignores [limits.conf] when starting services, etc.  It
# has its own configuration which we'll update below.  Note that [limits.conf]
# is still important because the kernel uses those settings when starting
# [systemd] as the init process 1.

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
#                 for maxlogin limit
#        - NOTE: group and wildcard limits are not applied to root.
#          To apply a limit to the root user, <domain> must be
#          the literal username root.
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
#  This file is part of systemd.
#
#  systemd is free software; you can redistribute it and/or modify it
#  under the terms of the GNU Lesser General Public License as published by
#  the Free Software Foundation; either version 2.1 of the License, or
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
# Tweak some kernel settings.  I extracted this file from a clean Ubuntu install
# and then made the changes marked by the ""# TWEAK"" comment.

cat <<EOF > /etc/sysctl.conf
#
# /etc/sysctl.conf - Configuration file for setting system variables
# See /etc/sysctl.d/ for additional system variables.
# See sysctl.conf (5) for information.
#

#kernel.domainname = example.com

# Uncomment the following to stop low-level messages on console
#kernel.printk = 3 4 1 3

##############################################################3
# Functions previously found in netbase

# Uncomment the next two lines to enable Spoof protection (reverse-path filter)
# Turn on Source Address Verification in all interfaces to
# prevent some spoofing attacks
#net.ipv4.conf.default.rp_filter = 1
#net.ipv4.conf.all.rp_filter = 1

# Uncomment the next line to enable TCP/IP SYN cookies
# See http://lwn.net/Articles/277146/
# Note: This may impact IPv6 TCP sessions too
#net.ipv4.tcp_syncookies = 1

# Uncomment the next line to enable packet forwarding for IPv4
#net.ipv4.ip_forward = 1

# Uncomment the next line to enable packet forwarding for IPv6
#  Enabling this option disables Stateless Address Autoconfiguration
#  based on Router Advertisements for this host
#net.ipv6.conf.all.forwarding = 1

###################################################################
# Additional settings - these settings can improve the network
# security of the host and prevent against some network attacks
# including spoofing attacks and man in the middle attacks through
# redirection. Some network environments, however, require that these
# settings are disabled so review and enable them as needed.
#
# Do not accept ICMP redirects (prevent MITM attacks)
#net.ipv4.conf.all.accept_redirects = 0
#net.ipv6.conf.all.accept_redirects = 0
# _or_
# Accept ICMP redirects only for gateways listed in our default
# gateway list (enabled by default)
# net.ipv4.conf.all.secure_redirects = 1
#
# Do not send ICMP redirects (we are not a router)
#net.ipv4.conf.all.send_redirects = 0
#
# Do not accept IP source route packets (we are not a router)
#net.ipv4.conf.all.accept_source_route = 0
#net.ipv6.conf.all.accept_source_route = 0
#
# Log Martian Packets
#net.ipv4.conf.all.log_martians = 1
#

###################################################################
# Magic system request Key
# 0=disable, 1=enable all
# Debian kernels have this set to 0 (disable the key)
# See https://www.kernel.org/doc/Documentation/sysrq.txt
# for what other values do
#kernel.sysrq = 1

###################################################################
# Protected links
#
# Protects against creating or following links under certain conditions
# Debian kernels have both set to 1 (restricted) 
# See https://www.kernel.org/doc/Documentation/sysctl/fs.txt
#fs.protected_hardlinks = 0
#fs.protected_symlinks = 0

###################################################################
# TWEAK: neonKUBE settings:

# Explicitly set the maximum number of file descriptors for the
# entire system.  This looks like it defaults to [1048576] for
# Ubuntu 20.04 so we're going to pin this value to enforce
# consistency across Linux updates, etc.
fs.file-max = 1048576

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

# Set the network packet receive queue.
net.core.netdev_max_backlog = 2000

# Specify the range of TCP ports that can be used by client sockets.
net.ipv4.ip_local_port_range = 9000 65535

# Set the pending TCP connection backlog.
net.core.somaxconn = 25000
net.ipv4.tcp_max_syn_backlog = 25000

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
# TWEAK: Setting overrides recommended for custom Google Cloud images
#
#   https://cloud.google.com/compute/docs/images/building-custom-os

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
#net.ipv4.ip_forward = 0

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

#cat <<EOF > /etc/modprobe.d/nf_conntrack.conf
# Explicitly set the maximum number of TCP connections that iptables can track.
# Note that this number is multiplied by 8 to obtain the connection count.
#options nf_conntrack hashsize = 393216
#EOF

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
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
#Compress=yes
#Seal=yes
#SplitMode=uid
#SyncIntervalSec=5m
#RateLimitInterval=30s
#RateLimitBurst=1000
#SystemMaxUse=
#SystemKeepFree=
#SystemMaxFileSize=
#SystemMaxFiles=100
#RuntimeMaxUse=
#RuntimeKeepFree=
#RuntimeMaxFileSize=
#RuntimeMaxFiles=100
MaxRetentionSec=345600
#MaxFileSec=1month
#ForwardToSyslog=yes
#ForwardToKMsg=no
#ForwardToConsole=no
#ForwardToWall=yes
#TTYPath=/dev/console
#MaxLevelStore=debug
#MaxLevelSyslog=debug
#MaxLevelKMsg=notice
#MaxLevelConsole=info
#MaxLevelWall=emerg
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

cat <<EOF > {KubeNodeFolders.Bin}/neon-cleaner
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-cleaner
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

# This is a simple service script that periodically cleans accumulated files
# on the cluster node including:
#
#   1. Shred and delete the root account's [.bash-history] file 
#      as a security measure.  These commands could include
#      sensitive information such as credentials, etc.
#
#   2. Purge temporary Neon command files uploaded by LinuxSshProxy.  These
#      are located within folder beneath [/dev/shm/neonkube/cmd].  Although
#      LinuxSshProxy removes these files after commands finish executing, it
#      is possible to see these accumulate if the session was interrupted.
#      We'll purge folders and files older than one day.
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

chmod 700 {KubeNodeFolders.Bin}/neon-cleaner

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
ExecStart={KubeNodeFolders.Bin}/neon-cleaner
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
        /// Installs the Helm charts as a single ZIP archive written to the 
        /// neonKUBE Helm folder.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallHelmArchive(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            using (var ms = new MemoryStream())
            {
                controller.LogProgress(this, verb: "install", message: "helm charts (zip)");

                var helmFolder = KubeHelper.Resources.GetDirectory("/Helm");    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1121

                helmFolder.Zip(ms, searchOptions: SearchOption.AllDirectories, zipOptions: StaticZipOptions.LinuxLineEndings);

                ms.Seek(0, SeekOrigin.Begin);
                Upload(LinuxPath.Combine(KubeNodeFolders.Helm, "charts.zip"), ms, permissions: "660");
            }
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
                    controller.LogProgress(this, verb: "install", message: "ipvs");

                    var setupScript =
$@"
{KubeNodeFolders.Bin}/safe-apt-get update -y
{KubeNodeFolders.Bin}/safe-apt-get install -y ipset ipvsadm
";
                    SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
                
        }

        /// <summary>
        /// Installs the <b>CRI-O</b> container runtime.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallCriO(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("setup/cri-o",
                () =>
                {
                    controller.LogProgress(this, verb: "install", message: "cri-o");

                    if (hostEnvironment != HostingEnvironment.Wsl2)
                    {
                        // This doesn't work with WSL2 because the Microsoft Linux kernel doesn't
                        // include these modules.  We'll set them up for the other environments.

                        var moduleScript =
@"
# Create the .conf file to load required modules during boot.

set -euo pipefail

cat <<EOF > /etc/modules-load.d/crio.conf
overlay
br_netfilter
EOF

# ...and load them explicitly now.

modprobe overlay
modprobe br_netfilter

sysctl --system
";                      SudoCommand(CommandBundle.FromScript(moduleScript));
                    }

                    var setupScript =
$@"
set -euo pipefail

# Configure the CRI-O package respository.

OS=xUbuntu_20.04
VERSION={KubeVersions.CrioVersion}

cat <<EOF > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
deb https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${{OS}}/ /
EOF

cat <<EOF > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable:cri-o:${{VERSION}}.list
deb http://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable:/cri-o:/${{VERSION}}/${{OS}}/ /
EOF

curl {KubeHelper.CurlOptions} https://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/${{OS}}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers.gpg add -
curl {KubeHelper.CurlOptions} https://download.opensuse.org/repositories/devel:kubic:libcontainers:stable:cri-o:${{VERSION}}/${{OS}}/Release.key | apt-key --keyring /etc/apt/trusted.gpg.d/libcontainers-cri-o.gpg add -

# Install the CRI-O packages.

{KubeNodeFolders.Bin}/safe-apt-get update -y
{KubeNodeFolders.Bin}/safe-apt-get install -y cri-o cri-o-runc

# Generate the CRI-O configurations.

NEON_REGISTRY={KubeConst.LocalClusterRegistry}

cat <<EOF > /etc/containers/registries.conf
[[registry]]
prefix = ""${{NEON_REGISTRY}}""
insecure = false
blocked = false
location = ""${{NEON_REGISTRY}}""

[[registry.mirror]]
location = ""{KubeConst.LocalClusterRegistry}""

[[registry]]
prefix = ""docker.io""
insecure = false
blocked = false
location = ""docker.io""

[[registry.mirror]]
location = ""{KubeConst.LocalClusterRegistry}""

[[registry]]
prefix = ""quay.io""
insecure = false
blocked = false
location = ""quay.io""

[[registry.mirror]]
location = ""{KubeConst.LocalClusterRegistry}""
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
#root = ""/var/lib/containers/storage""

# Path to the ""run directory"". CRI-O stores all of its state in this directory.
#runroot = ""/var/run/containers/storage""

# Storage driver used to manage the storage of images and containers. Please
# refer to containers-storage.conf(5) to see all available storage drivers.
#storage_driver = """"

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
#default_ulimits = [
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
#If it is empty or commented out, only the devices
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
#      override file, where users can either add in their own default mounts, or
#      override the default mounts shipped with the package.
#
#   2) /usr/share/containers/mounts.conf: This is the default file read for
#      mounts. If you want CRI-O to read from a different, specific mounts file,
#      you can change the default_mounts_file. Note, if this is done, CRI-O will
#      only add mounts it finds in this file.
#
#default_mounts_file = """"

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
#  runtime_path = ""/path/to/the/executable""
#  runtime_type = ""oci""
#  runtime_root = ""/path/to/the/root""
#
# Where:
# - runtime-handler: name used to identify the runtime
# - runtime_path (optional, string): absolute path to the runtime executable in
#   the host filesystem. If omitted, the runtime-handler identifier should match
#   the runtime executable name, and the runtime executable should be placed
#   in $PATH.
# - runtime_type (optional, string): type of runtime, one of: ""oci"", ""vm"". If
#   omitted, an ""oci"" runtime is assumed.
# - runtime_root (optional, string): root directory for storage of containers
#   state.


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
pause_image = ""{KubeConst.LocalClusterRegistry}/pause:{KubeVersions.PauseVersion}""

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
#insecure_registries = ""[]""

# Controls how image volumes are handled. The valid values are mkdir, bind and
# ignore; the latter will ignore volumes entirely.
image_volumes = ""mkdir""

# List of registries to be used when pulling an unqualified image (e.g.,
# ""alpine:latest""). By default, registries is set to ""docker.io"" for
# compatibility reasons. Depending on your workload and usecase you may add more
# registries (e.g., ""quay.io"", ""registry.fedoraproject.org"",
# ""registry.opensuse.org"", etc.).
#registries = [
# ]


# The crio.network table containers settings pertaining to the management of
# CNI plugins.
[crio.network]

# The default CNI network name to be selected. If not set or """", then
# CRI-O will pick-up the first one found in network_dir.
# cni_default_network = """"

# Path to the directory where CNI configuration files are located.
network_dir = ""/etc/cni/net.d/""

# Paths to directories where CNI plugin binaries are located.
plugin_dirs = [
        ""/opt/cni/bin/"",
]

# A necessary configuration for Prometheus based metrics retrieval
[crio.metrics]

# Globally enable or disable metrics support.
enable_metrics = false

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

                });
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
                    controller.LogProgress(this, verb: "install", message: "podman");

                    var setupScript =
$@"
source /etc/os-release
echo ""deb http://download.opensuse.org/repositories/devel:/kubic:/libcontainers:/stable/xUbuntu_${{VERSION_ID}}/ /"" > /etc/apt/sources.list.d/devel:kubic:libcontainers:stable.list
wget -nv https://download.opensuse.org/repositories/devel:kubic:libcontainers:stable/xUbuntu_${{VERSION_ID}}/Release.key -O- | apt-key add -
{KubeNodeFolders.Bin}/safe-apt-get update -qq
{KubeNodeFolders.Bin}/safe-apt-get install -yq podman-rootless
ln -s /usr/bin/podman /bin/docker

# Prevent the package manager from automatically upgrading these components.

set +e      # Don't exit if the next command fails
apt-mark hold podman
";
                    SudoCommand(CommandBundle.FromScript(setupScript), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the Helm client.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInstallHelm(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/helm-client",
                () =>
                {
                    controller.LogProgress(this, verb: "install", message: "helm client");

                    var script =
$@"
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

                            targetTag = $"neonkube-{KubeVersions.NeonKubeVersion}";
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

            InvokeIdempotent("setup/install-kubernetes",
                () =>
                {
                    var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

                    // We need some custom configuration on WSL2 inspired by the 
                    // Kubernetes-IN-Docker (KIND) project:
                    //
                    //      https://d2iq.com/blog/running-kind-inside-a-kubernetes-cluster-for-continuous-integration

                    if (hostingEnvironment == HostingEnvironment.Wsl2)
                    {
                        // We need to disable IPv6 on WSL2.  We're going to accomplish this by
                        // writing a config file to be included last by [/etc/sysctl.conf].

                        var confScript =
@"
cat <<EOF > /etc/sysctl.d/990-wsl2-no-ipv6
# neonKUBE needs to disable IPv6 when hosted on WSL2.

net.ipv6.conf.all.disable_ipv6=1
net.ipv6.conf.default.disable_ipv6=1
net.ipv6.conf.lo.disable_ipv6=1
EOF

chmod 744 /etc/sysctl.d/990-wsl2-no-ipv6

sysctl -p /etc/sysctl.d/990-wsl2-no-ipv6
";
                        SudoCommand(CommandBundle.FromScript(confScript), RunOptions.FaultOnError);
                    }

                    // Perform the install.

                    var mainScript =
$@"
curl {KubeHelper.CurlOptions} https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
echo ""deb https://apt.kubernetes.io/ kubernetes-xenial main"" > /etc/apt/sources.list.d/kubernetes.list
{KubeNodeFolders.Bin}/safe-apt-get update

{KubeNodeFolders.Bin}/safe-apt-get install -yq kubelet={KubeVersions.KubeletPackageVersion}
{KubeNodeFolders.Bin}/safe-apt-get install -yq kubeadm={KubeVersions.KubeAdminPackageVersion}
{KubeNodeFolders.Bin}/safe-apt-get install -yq kubectl={KubeVersions.KubeCtlPackageVersion}

# Prevent the package manager these components from starting automatically.

set +e      # Don't exit if the next command fails
apt-mark hold kubeadm kubectl kubelet

# Configure kublet:

mkdir -p /opt/cni/bin
mkdir -p /etc/cni/net.d

echo KUBELET_EXTRA_ARGS=--network-plugin=cni --cni-bin-dir=/opt/cni/bin --cni-conf-dir=/etc/cni/net.d --feature-gates=\""AllAlpha=false,RunAsGroup=true\"" --container-runtime=remote --cgroup-driver=systemd --container-runtime-endpoint='unix:///var/run/crio/crio.sock' --runtime-request-timeout=5m > /etc/default/kubelet

# Stop and disable [kubelet] for now.  We'll enable this later during cluster setup.

systemctl daemon-reload
systemctl stop kubelet
systemctl disable kubelet
";
                    controller.LogProgress(this, verb: "install", message: "kubernetes");

                    SudoCommand(CommandBundle.FromScript(mainScript), RunOptions.Defaults | RunOptions.FaultOnError);

                    // Additional special configuration for WSL2.

                    if (hostingEnvironment == HostingEnvironment.Wsl2)
                    {
                        var script = KubeHelper.Resources.GetFile("/Scripts/wsl2-cgroup-setup.sh").ReadAllText();

                        SudoCommand(CommandBundle.FromScript(script));
                    }
                });
        }
    }
}
