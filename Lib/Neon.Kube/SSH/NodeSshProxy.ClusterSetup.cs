//-----------------------------------------------------------------------------
// FILE:        NodeSshProxy.ClusterSetup.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

// This file includes node configuration methods executed while configuring
// the node to be a cluster member.

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
using Neon.Kube.Hosting;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube.SSH
{
    public partial class NodeSshProxy<TMetadata> : LinuxSshProxy<TMetadata>
        where TMetadata : class
    {
        /// <summary>
        /// Performs low-level node initialization during cluster setup.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void NodeInitialize(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var hostEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            InvokeIdempotent("setup/clean-up",
                () =>
                {
                    // I'm not sure where this file comes from .  We're going
                    // to go ahead and delete this when present.

                    DeleteFile("/etc/ssh/sshd_config.d/50-neonkube.confE");
                });

            InvokeIdempotent("setup/sysstat",
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
#   # NEONKUBE tweaks
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
# TWEAK: NEONKUBE settings:

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
net.ipv4.ip_local_port_range = 10000 65535

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
net.netfilter.NK_conntrack_max=1000000
net.NK_conntrack_max=1000000
net.netfilter.NK_conntrack_expect_max=1000
net.netfilter.NK_conntrack_buckets=250000

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

# cat <<EOF > /etc/modprobe.d/NK_conntrack.conf
# Explicitly set the maximum number of TCP connections that iptables can track.
# Note that this number is multiplied by 8 to obtain the connection count.
# options NK_conntrack hashsize = 393216
# EOF

cat > /etc/modules <<EOF
ip_vs
ip_vs_rr
ip_vs_wrr
ip_vs_sh
NK_conntrack
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
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
            InvokeIdempotent("setup/initialize",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "node (low-level)");

                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures NTP and also installs some tool scripts for managing this.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void SetupConfigureNtp(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/ntp",
                () =>
                {
                    controller.LogProgress(this, verb: "configure", message: "ntp");

                    var clusterDefinition = this.Cluster.SetupState.ClusterDefinition;
                    var nodeDefinition    = this.NodeDefinition;

                    var script =
$@"
mkdir -p /var/log/ntp

cat <<EOF > /etc/ntp.conf
# For more information about this file, see the man pages
# ntp.conf(5), ntp_acc(5), ntp_auth(5), ntp_clock(5), ntp_misc(5), ntp_mon(5).

# Specifies that NTP will implicitly trust the time source, 
# even if it is way off from the current system time.  This
# can happen when:
#
#       1. The BIOS time is set incorrectly
#       2. The machine is a VM that woke up from a long sleep
#       3. The time source is messed up
#       4. Somebody is screwing with us by sending bad time responses
#
# Normally, NTP is configured to panic when it sees a difference
# of 1000 seconds or more between the source and the system time
# and NTP will log an error and terminate.
#
# NTP is configured to start with the [-g] option, which will 
# ignore 1000+ second differences when the service starts.  This
# handles issues #1 and #2 above.
#
# The setting below disables the 1000 second check and also allows
# NTP to modify the system clock in steps of 1 second (normally,
# this is much smaller).

# \$todo(jefflill):
#
# This was the default setting when NTP was installed but I'm not 
# entirely sure that this is what we'd want for production.  The
# [man ntp.conf] page specifically recommends not messing with
# these defaults.

tinker panic 0 dispersion 1.000

# Specifies the file used to track the frequency offset of The
# local clock oscillator.

driftfile /var/lib/ntp/ntp.drift

# Configure a log file.

logfile /var/log/ntp/ntp.log
 
# Permit time synchronization with our time source, but do not
# permit the source to query or modify the service on this system.

restrict default kod nomodify notrap nopeer noquery
restrict -6 default kod nomodify notrap nopeer noquery

# Permit all access over the loopback interface.  This could
# be tightened as well, but to do so would effect some of
# the administrative functions.

restrict 127.0.0.1
restrict -6 ::1

# Hosts on local networks are less restricted.

restrict 10.0.0.0 mask 255.0.0.0 nomodify notrap
restrict 169.254.0.0 mask 255.255.0.0 nomodify notrap
restrict 172.16.0.0 mask 255.255.0.0 nomodify notrap
restrict 192.168.0.0 mask 255.255.0.0 nomodify notrap

# Use local system time when the time sources are not reachable.

server  127.127.1.0 # local clock
fudge   127.127.1.0 stratum 10

# Specify the time sources.

EOF

# Append the time sources to the configuration file.
#
# We're going to prefer the first source.  This will generally
# be the first control-plane node.  The nice thing about this
# the worker nodes will be using the same time source on
# the local network, so the clocks should be very close to
# being closely synchronized.

sources=({GetNtpSources()})

echo ""*** TIME SOURCES = ${{sources}}"" 1>&2

preferred_set=false

for source in ""${{sources[@]}}"";
do
    if ! ${{preferred_set}} ; then
        echo ""server $source burst iburst prefer"" >> /etc/ntp.conf
        preferred_set=true
    else
        echo ""server $source burst iburst"" >> /etc/ntp.conf
    fi
done

# Generate the [{KubeNodeFolder.Bin}/update-time] script.

cat <<EOF > {KubeNodeFolder.Bin}/update-time
#!/bin/bash
#------------------------------------------------------------------------------
# This script stops the NTP time service and forces an immediate update.  This 
# is called from the NTP init.d script (as modified below) to ensure that we 
# obtain the current time on boot (even if the system clock is way out of sync).
# This may also be invoked manually.
#
# Usage:
#
#       update-time [--norestart]
#
#       --norestart - Don't restart NTP

. $<load-cluster-conf-quiet>

restart=true

for arg in \$@
do
    if [ ""\${{arg}}"" = ""--norestart"" ] ; then
        restart=false
    fi
done

if \${{restart}} ; then
    service ntp stop
fi

ntpdate ${{sources[@]}}

if \${{restart}} ; then
    service ntp start
fi
EOF

chmod 700 {KubeNodeFolder.Bin}/update-time

# Edit the NTP [/etc/init.d/ntp] script to initialize the hardware clock and
# call [update-time] before starting NTP.

cat <<""EOF"" > /etc/init.d/ntp
#!/bin/sh

### BEGIN INIT INFO
# Provides:        ntp
# Required-Start:  $network $remote_fs $syslog
# Required-Stop:   $network $remote_fs $syslog
# Default-Start:   2 3 4 5
# Default-Stop:    1
# Short-Description: Start NTP daemon
### END INIT INFO

PATH=/sbin:/bin:/usr/sbin:/usr/bin

. /lib/lsb/init-functions

DAEMON=/usr/sbin/ntpd
PIDFILE=/var/run/ntpd.pid

test -x $DAEMON || exit 5

if [ -r /etc/default/ntp ]; then
    . /etc/default/ntp
fi

if [ -e /var/lib/ntp/ntp.conf.dhcp ]; then
    NTPD_OPTS=""$NTPD_OPTS -c /var/lib/ntp/ntp.conf.dhcp""
fi

LOCKFILE=/var/lock/ntpdate

lock_ntpdate() {{
    if [ -x /usr/bin/lockfile-create ]; then
        lockfile-create $LOCKFILE
        lockfile-touch $LOCKFILE &
        LOCKTOUCHPID=""$!""
    fi
}}

unlock_ntpdate() {{
    if [ -x /usr/bin/lockfile-create ] ; then
        kill $LOCKTOUCHPID
        lockfile-remove $LOCKFILE
    fi
}}

RUNASUSER=ntp
UGID=$(getent passwd $RUNASUSER | cut -f 3,4 -d:) || true
if test ""$(uname -s)"" = ""Linux""; then
        NTPD_OPTS=""$NTPD_OPTS -u $UGID""
fi

case $1 in
    start)
        log_daemon_msg ""Starting NTP server"" ""ntpd""
        if [ -z ""$UGID"" ]; then
            log_failure_msg ""user \""$RUNASUSER\"" does not exist""
            exit 1
        fi

        #------------------------------
        # This is the modification.

        # This bit of voodoo disables Hyper-V time synchronization with The
        # host server.  We don't want this because the cluster is doing its
        # own time management and time sync will fight us.  This is described
        # here:
        #
        # https://social.msdn.microsoft.com/Forums/en-US/8c0a1026-0b02-405a-848e-628e68229eaf/i-have-a-lot-of-time-has-been-changed-in-the-journal-of-my-linux-boxes?forum=WAVirtualMachinesforWindows

        log_daemon_msg ""Start: Disabling Hyper-V time synchronization"" ""ntpd""
        echo 2dd1ce17-079e-403c-b352-a1921ee207ee > /sys/bus/vmbus/drivers/hv_util/unbind
        log_daemon_msg ""Finished: Disabling Hyper-V time synchronization"" ""ntpd""
        
        log_daemon_msg ""Start: Updating current time"" ""ntpd""
        {KubeNodeFolder.Bin}/update-time --norestart
        log_daemon_msg ""Finished: Updating current time"" ""ntpd""

        #------------------------------

        lock_ntpdate
        start-stop-daemon --start --quiet --oknodo --pidfile $PIDFILE --startas $DAEMON -- -p $PIDFILE $NTPD_OPTS
        status=$?
        unlock_ntpdate
        log_end_msg $status
        ;;
    stop)
        log_daemon_msg ""Stopping NTP server"" ""ntpd""
        start-stop-daemon --stop --quiet --oknodo --pidfile $PIDFILE
        log_end_msg $?
        rm -f $PIDFILE
        ;;
    restart|force-reload)
        $0 stop && sleep 2 && $0 start
        ;;
    try-restart)
        if $0 status >/dev/null; then
            $0 restart
        else
            exit 0
        fi
        ;;
    reload)
        exit 3
        ;;
    status)
        status_of_proc $DAEMON ""NTP server""
        ;;
    *)
        echo ""Usage: $0 {{start|stop|restart|try-restart|force-reload|status}}""
        exit 2
        ;;
esac
EOF

# Restart NTP to ensure that we've picked up the current time.

service ntp restart
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Configures the global environment variables that describe the configuration 
        /// of the server within the cluster.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void ConfigureEnvironmentVariables(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            controller.LogProgress(this, verb: "configure", message: "environment");

            var clusterDefinition = Cluster.SetupState.ClusterDefinition;
            var nodeDefinition    = NeonHelper.CastTo<NodeDefinition>(Metadata);

            // We're going to append the new variables to the existing Linux [/etc/environment] file.

            var sb = new StringBuilder();

            // Append all of the existing environment variables except for those
            // whose names start with "NEON_" to make the operation idempotent.
            //
            // Note that we're going to special case PATH to add any Neon
            // related directories.

            using (var currentEnvironmentStream = new MemoryStream())
            {
                Download("/etc/environment", currentEnvironmentStream);

                currentEnvironmentStream.Position = 0;

                using (var reader = new StreamReader(currentEnvironmentStream))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (line.StartsWith("PATH="))
                        {
                            if (!line.Contains(KubeNodeFolder.Bin))
                            {
                                sb.AppendLine(line + $":/snap/bin:{KubeNodeFolder.Bin}");
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

            sb.AppendLine($"NEON_CLUSTER={clusterDefinition.Name}");
            sb.AppendLine($"NEON_DATACENTER={clusterDefinition.Datacenter.ToLowerInvariant()}");
            sb.AppendLine($"NEON_ENVIRONMENT={clusterDefinition.Purpose.ToString().ToLowerInvariant()}");

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

            sb.AppendLine($"NEON_NODE_NAME={Name}");

            if (nodeDefinition != null)
            {
                sb.AppendLine($"NEON_NODE_ROLE={nodeDefinition.Role}");
                sb.AppendLine($"NEON_NODE_IP={nodeDefinition.Address}");
                sb.AppendLine($"NEON_NODE_HDD={nodeDefinition.Labels.StorageOSDiskHDD.ToString().ToLowerInvariant()}");
            }

            sb.AppendLine($"NEON_BIN_FOLDER={KubeNodeFolder.Bin}");
            sb.AppendLine($"NEON_CONFIG_FOLDER={KubeNodeFolder.Config}");
            sb.AppendLine($"NEON_SETUP_FOLDER={KubeNodeFolder.Setup}");
            sb.AppendLine($"NEON_STATE_FOLDER={KubeNodeFolder.State}");
            sb.AppendLine($"NEON_RUN_FOLDER={KubeNodeFolder.NeonRun}");
            sb.AppendLine($"NEON_TMPFS_FOLDER={KubeNodeFolder.Tmpfs}");

            // Kubernetes related variables for control-plane nodes.

            if (nodeDefinition.IsControlPane)
            {
                sb.AppendLine($"KUBECONFIG=/etc/kubernetes/admin.conf");
            }

            // Upload the new environment to the server.

            UploadText("/etc/environment", sb, tabStop: 4);
        }

        /// <summary>
        /// Updates the node hostname and DNS configuration for the host,
        /// including A records for the <b>hostname</b>, <b>kubernetes-control-plane</b>,
        /// and the <b>Harbor Registry</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        private void ConfigureLocalHosts(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            controller.LogProgress(this, verb: "configure", message: "hostname");
            
            // Update the hostname.

            SudoCommand($"hostnamectl set-hostname {Name}");

            // Update the [/etc/hosts] file to resolve the new hostname.

            // $hack(jefflill):
            //
            // We need to obtain the private address of the node when [TMetadata]
            // is a [NodeDefinition] since the [Address] for cloud cluster nodes
            // will reference the external load balancer IP.
            //
            // We'll use [Address] when the [TMetadata != NodeDefinition].

            var nodeAddress    = Address.ToString();
            var nodeDefinition = Metadata as NodeDefinition;

            if (nodeDefinition != null)
            {
                nodeAddress = nodeDefinition.Address;
            }

            var separator = new string(' ', Math.Max(16 - nodeAddress.Length, 1));
            var sbHosts   = new StringBuilder();

            sbHosts.Append(
$@"
# IPv4 hosts:

127.0.0.1       localhost

# IPv6 hosts:

::1             localhost ip6-localhost ip6-loopback
ff02::1         ip6-allnodes
ff02::2         ip6-allrouters

# Configured for NEONKUBE:

127.0.0.1       kubernetes-control-plane
{nodeAddress}{separator}{Name}
{nodeAddress}{separator}{KubeConst.LocalClusterRegistryHostName}
");
            UploadText("/etc/hosts", sbHosts, tabStop: 4, outputEncoding: Encoding.UTF8);
        }

        /// <summary>
        /// Configures cluster package manager caching.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void SetupPackageProxy(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var nodeDefinition    = NeonHelper.CastTo<NodeDefinition>(Metadata);
            var clusterDefinition = Cluster.SetupState.ClusterDefinition;
            var hostingManager    = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);

            InvokeIdempotent("setup/package-caching",
                () =>
                {
                    controller.LogProgress(this, verb: "install", message: "apt package proxy");

                    // Configure the [apt-cacher-ng] package proxy service on control-plane nodes.

                    if (NodeDefinition.Role == NodeRole.ControlPlane)
                    {
                        var proxyServiceScript =
$@"
set -eou pipefail   # Enable full failure detection

{KubeConst.SafeAptGetToolPath} update
{KubeConst.SafeAptGetToolPath} install -yq apt-cacher-ng

# Configure the cache to pass-thru SSL requests
# and then restart.

echo ""PassThroughPattern:^.*:443$"" >> /etc/apt-cacher-ng/acng.conf
systemctl restart apt-cacher-ng

set -eo pipefail    # Revert back to partial failure detection

# Give the proxy service a chance to start.

sleep 5
";
                        SudoCommand(CommandBundle.FromScript(proxyServiceScript), RunOptions.FaultOnError);
                    }

                    var sbPackageProxies = new StringBuilder();

                    if (clusterDefinition.PackageProxy != null)
                    {
                        foreach (var proxyEndpoint in clusterDefinition.PackageProxy.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        {
                            sbPackageProxies.AppendWithSeparator(proxyEndpoint);
                        }
                    }

                    // Configure the package manager to use the first control-plane as the proxy by default,
                    // failing over to the other control-plane nodes (in order) when necessary.

                    var proxySelectorScript =
$@"
# Configure APT proxy selection.

echo {sbPackageProxies} > {KubeNodeFolder.Config}/package-proxy

cat <<EOF > /usr/local/bin/get-package-proxy
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:        get-package-proxy
# CONTRIBUTOR: Generated by [neon-cli] during cluster setup.
#
# This script determine which (if any) configured APT proxy caches are running
# and returns its endpoint or ""DIRECT"" if none of the proxies are available and 
# the distribution's mirror should be accessed directly.  This uses the
# [{KubeNodeFolder.Config}/package-proxy] file to obtain the list of proxies.
#
# This is called when the following is specified in the APT configuration,
# as we do further below:
#
#       Acquire::http::Proxy-Auto-Detect ""/usr/local/bin/get-package-proxy"";
#
# See this link for more information:
#
#       https://trent.utfs.org/wiki/Apt-get#Failover_Proxy
NEON_PACKAGE_PROXY=$(cat {KubeNodeFolder.Config}/package-proxy)
if [ ""\${{NEON_PACKAGE_PROXY}}"" == """" ] ; then
    echo DIRECT
    exit 0
fi
for proxy in ${{NEON_PACKAGE_PROXY}}; do
    if nc -w1 -z \${{proxy/:/ }}; then
        echo http://\${{proxy}}/
        exit 0
    fi
done
echo DIRECT
exit 0
EOF

chmod 775 /usr/local/bin/get-package-proxy

cat <<EOF > /etc/apt/apt.conf
//-----------------------------------------------------------------------------
// FILE:        /etc/apt/apt.conf
// CONTRIBUTOR: Generated by during NEONKUBE cluster setup.
//
// This file configures APT on the local machine to proxy requests through the
// [apt-cacher-ng] instance(s) at the configured.  This uses the [/usr/local/bin/get-package-proxy] 
// script to select a working PROXY if there are more than one, or to go directly to the package
// mirror if none of the proxies are available.
//
// Presumably, this cache is running on the local network which can dramatically
// reduce external network traffic to the APT mirrors and improve cluster setup 
// and update performance.

Acquire::http::Proxy-Auto-Detect ""/usr/local/bin/get-package-proxy"";
EOF
";
                    SudoCommand(CommandBundle.FromScript(proxySelectorScript), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="clusterManifest">The cluster manifest.</param>
        public void SetupNode(ISetupController controller, ClusterManifest clusterManifest)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var nodeDefinition    = NeonHelper.CastTo<NodeDefinition>(Metadata);
            var clusterDefinition = Cluster.SetupState.ClusterDefinition;
            var hostingManager    = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);

            InvokeIdempotent("setup/node",
                () =>
                {
                    controller.ThrowIfCancelled();
                    PrepareNode(controller);

                    controller.ThrowIfCancelled();
                    ConfigureEnvironmentVariables(controller);

                    controller.ThrowIfCancelled();
                    ConfigureLocalHosts(controller);

                    controller.ThrowIfCancelled();
                    BaseCreateKubeFolders(controller);

                    controller.ThrowIfCancelled();
                    SetupPackageProxy(controller);

                    controller.ThrowIfCancelled();
                    NodeInitialize(controller);

                    controller.ThrowIfCancelled();
                    NodeInstallCriO(controller, clusterManifest);

                    controller.ThrowIfCancelled();
                    NodeInstallPodman(controller);

                    controller.ThrowIfCancelled();
                    NodeInstallHelm(controller);

                    controller.ThrowIfCancelled();
                    NodeInstallKubernetes(controller);

                    controller.ThrowIfCancelled();
                    SetupKubelet(controller);

                    controller.ThrowIfCancelled();
                    InstallCiliumCli(controller);

                    controller.ThrowIfCancelled();
                    InstallIstioCli(controller);
                });

            controller.SetGlobalStepStatus();
        }

        /// <summary>
        /// Configures the <b>kubelet</b> service.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <remarks>
        /// <note>
        /// Kubelet is installed in <see cref="NodeSshProxy{TMetadata}.NodeInstallKubernetes"/> when configuring
        /// the node image and is then configured for the cluster here.
        /// </note>
        /// </remarks>
        public void SetupKubelet(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            InvokeIdempotent("setup/kubelet",
                () =>
                {
                    controller.LogProgress(this, verb: "install", message: "kubelet");

                    // Configure the image GC thesholds.  We'll stick with the defaults of 80/85% of the
                    // OS disk for most nodes and customize this at 93/95% for clusters with OS disks
                    // less than 64GB.  We want higher thesholds for smaller disks to leave more space
                    // for user images and local volumes, especially for the NEONDESKTOP cluster.
                    //
                    // We're going to use this command to retrieve the node's disk information:
                    //
                    //       fdisk --list -o Device,Size,Type | grep ^/dev
                    //
                    // which will produce output like:
                    //
                    //      /dev/sda1     1M BIOS boot
                    //      /dev/sda2   128G Linux filesystem
                    //
                    // We're going to look for the line with "Linux filesystem" and and then extract and
                    // parse the size in the second column to decide which GC thresholds to use.
                    //
                    // $note(jefflill):
                    //
                    // This assumes that there's only one Linux filesystem for each cluster node which is 
                    // currently the case for all NEONKUBE clusters.  The cStor disks are managed by OpenEBS
                    // and will not be reported as a file system.  I'll add an assert to verify this to
                    // make this easier diagnose in the future if we decide to allow multiple file systems.
                    //
                    // $todo(jefflill):
                    //
                    // We're hardcoding this now based on the current node disk size but eventually it
                    // might make sense to add settings to the cluster definition so user can override
                    // this, perhaps customizing specfic nodes.

                    var imageLowGcThreshold  = 80;
                    var imageHighGcThreshold = 85;
                    var diskSize             = 0L;
                    var result               = SudoCommand(CommandBundle.FromScript("fdisk --list -o Device,Size,Type | grep ^/dev")).EnsureSuccess();

                    using (var reader = new StringReader(result.OutputText))
                    {
                        var filesystemCount = 0;

                        foreach (var line in reader.Lines())
                        {
                            if (!line.EndsWith("Linux filesystem") && !line.EndsWith("Linux"))
                            {
                                continue;
                            }

                            filesystemCount++;

                            var fields    = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var sizeField = fields[1];
                            var sizeUnit  = sizeField.Last();
                            var rawSize   = decimal.Parse(sizeField.Substring(0, sizeField.Length - 1));

                            switch (sizeUnit)
                            {
                                case 'G':

                                    diskSize = (long)(rawSize * ByteUnits.GibiBytes);
                                    break;

                                case 'T':

                                    diskSize = (long)(rawSize * ByteUnits.TebiBytes);
                                    break;

                                default:

                                    Covenant.Assert(false, () => $"Expecting partition size unit to be [G] or [T], not [{sizeUnit}].");
                                    break;
                            }
                        }

                        Covenant.Assert(filesystemCount == 1, () => $"Expected exactly [1] Linux file system but are seeing [{filesystemCount}].");
                    }

                    if (diskSize < 64 * ByteUnits.GibiBytes)
                    {
                        imageLowGcThreshold  = 93;
                        imageHighGcThreshold = 95;
                    }


                    var sbFeatures = new StringBuilder();

                    foreach (var featureGate in cluster.SetupState.ClusterDefinition.Kubernetes.FeatureGates
                        .Where(featureGate => KubeHelper.IsValidFeatureGate(featureGate.Key)))
                    {
                        sbFeatures.AppendWithSeparator($"{featureGate.Key}={NeonHelper.ToBoolString(featureGate.Value)}", ",");
                    }

                    var script =
$@"
set -euo pipefail

echo KUBELET_EXTRA_ARGS= \
    --cgroup-driver=systemd \
    --container-runtime-endpoint='unix:///var/run/crio/crio.sock' \
    --runtime-request-timeout=5m \
    --resolv-conf=/run/systemd/resolve/resolv.conf \
    --image-gc-low-threshold={imageLowGcThreshold} \
    --image-gc-high-threshold={imageHighGcThreshold} \
    --feature-gates={sbFeatures} \
    > /etc/default/kubelet

systemctl daemon-reload
systemctl restart kubelet
systemctl enable kubelet
";
                    SudoCommand(CommandBundle.FromScript(script), RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Helm doesn't like values with embedded commas (I believe it treats this as a list of key/value
        /// pairs which we don't ever take advantage of.  This method escapes any commas that are not already
        /// excaped in the value passed by prefixing the commas with a backslash <b>(\)</b>.
        /// </summary>
        /// <param name="value">The input value to be escaped.</param>
        /// <returns>The escaped string.</returns>
        private string EscapeHelmValueCommas(string value)
        {
            // We're going to make this easy by first replacing any already escaped commas
            // with a special string that'll be extermely unlikely to appear in any value,
            // then we'll replace any remaining commas with escaped command and then finally,
            // we'll replace any special strings with escaped commas.

            const string escapedCommaMarker = "[{209c1cb9-92a8-40d6-9c96-bd2432daee3d}]]";
            const string escapedComma       = @"\,";

            value = value.Replace(escapedComma, escapedCommaMarker);
            value = value.Replace(",", escapedComma);
            value = value.Replace(escapedCommaMarker, escapedComma);

            return value;
        }

        /// <summary>
        /// Installs a prepositioned Helm chart from a control-plane node.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="chartName">
        /// <para>
        /// The name of the Helm chart.
        /// </para>
        /// <note>
        /// Helm does not allow dashes <b>(-)</b> in chart names but to avoid problems
        /// with copy/pasting, we will automatically convert any dashes to underscores
        /// before installing the chart.  This is also nice because this means that the 
        /// chart name passed can be the same as the release name in the calling code.
        /// </note>
        /// </param>
        /// <param name="releaseName">
        /// Optionally specifies the component release name.  This defaults to the Helm
        /// chart name with any embedded underscores converted to dashes.
        /// </param>
        /// <param name="namespace">Optionally specifies the namespace where Kubernetes namespace where the Helm chart should be installed. This defaults to <b>default</b>.</param>
        /// <param name="prioritySpec">
        /// <para>
        /// Optionally specifies the Helm variable and priority class for any pods deployed by the chart.
        /// This needs to be specified as: <b>PRIORITYCLASSNAME</b> or <b>VALUENAME=PRIORITYCLASSNAME</b>,
        /// where <b>VALUENAME</b> optionally specifies the name of the Helm value and <b>PRIORITYCLASSNAME</b>
        /// is one of the priority class names defined by <see cref="PriorityClass"/>.
        /// </para>
        /// <note>
        /// The priority class will saved as the <b>priorityClassName</b> Helm value when no value
        /// name is specified.
        /// </note>
        /// </param>
        /// <param name="values">Optionally specifies Helm chart values.</param>
        /// <param name="progressMessage">Optionally specifies progress message.  This defaults to <paramref name="releaseName"/>.</param>
        /// <param name="timeout">Optionally specifies the timeout.  This defaults to <b>300 seconds</b>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the priority class specified by <paramref name="prioritySpec"/> is not defined by <see cref="PriorityClass"/>.</exception>
        /// <remarks>
        /// NEONKUBE images prepositions the Helm chart files embedded as resources in the <b>Resources/Helm</b>
        /// project folder to cluster node images as the <b>/lib/neonkube/helm/charts.zip</b> archive.  This
        /// method unzips that file to the same folder (if it hasn't been unzipped already) and then installs
        /// the helm chart (if it hasn't already been installed).
        /// </remarks>
        public async Task InstallHelmChartAsync(
            ISetupController                    controller,
            string                              chartName,
            string                              releaseName     = null,
            string                              @namespace      = "default",
            string                              prioritySpec    = null,
            Dictionary<string, object>          values          = null,
            string                              progressMessage = null,
            TimeSpan                            timeout         = default)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(chartName), nameof(chartName));

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(300);
            }

            chartName = chartName.Replace('-', '_');

            if (string.IsNullOrEmpty(releaseName))
            {
                releaseName = chartName.Replace("_", "-");
            }

            // Extract the Helm chart value name and priority class name from [priorityClass]
            // when passed.

            string priorityClassVariable = null;
            string priorityClassName     = null;

            if (!string.IsNullOrEmpty(prioritySpec))
            {
                var equalPos = prioritySpec.IndexOf('=');

                if (equalPos == -1)
                {
                    priorityClassVariable = "priorityClassName";
                    priorityClassName     = prioritySpec;
                }
                else
                {
                    priorityClassVariable = prioritySpec.Substring(0, equalPos).Trim();
                    priorityClassName     = prioritySpec.Substring(equalPos + 1).Trim();

                    if (string.IsNullOrEmpty(priorityClassVariable) || string.IsNullOrEmpty(priorityClassName))
                    {
                        throw new FormatException($"[{prioritySpec}] is not valid.  This must be formatted like: NAME=PRIORITYCLASSNAME");
                    }
                }

                PriorityClass.EnsureKnown(priorityClassName);
            }

            // Unzip the Helm chart archive if we haven't done so already.

            InvokeIdempotent("setup/helm-unzip",
                () =>
                {
                    controller.LogProgress(this, verb: "unzip", message: "helm charts");

                    var zipPath = LinuxPath.Combine(KubeNodeFolder.Helm, "charts.zip");
                    
                    SudoCommand($"unzip -o {zipPath} -d {KubeNodeFolder.Helm} || true");
                    SudoCommand($"rm -f {zipPath}");
                });

            // Install the chart when we haven't already done so.

            InvokeIdempotent($"setup/helm-install-{releaseName}",
                () =>
                {
                    controller.LogProgress(this, verb: "install", message: progressMessage ?? releaseName);

                    var valueOverrides = new StringBuilder();

                    if (!string.IsNullOrEmpty(priorityClassVariable))
                    {
                        valueOverrides.AppendWithSeparator($"--set {priorityClassVariable}={priorityClassName}");
                    }

                    if (values != null)
                    {
                        foreach (var value in values)
                        {
                            if (value.Value == null)
                            {
                                valueOverrides.AppendWithSeparator($"--set {value.Key}=null");
                                continue;
                            }

                            var valueType = value.Value.GetType();

                            switch (value.Value)
                            {
                                case string s:

                                    valueOverrides.AppendWithSeparator($"--set-string {value.Key}=\"{EscapeHelmValueCommas((string)value.Value)}\"");
                                    break;

                                case Boolean b:

                                    valueOverrides.AppendWithSeparator($"--set {value.Key}=\"{value.Value.ToString().ToLower()}\"");
                                    break;

                                default:

                                    valueOverrides.AppendWithSeparator($"--set {value.Key}={value.Value}");
                                    break;
                            }
                        }
                    }

                    SudoCommand($"helm install {releaseName} --debug --namespace {@namespace} -f {KubeNodeFolder.Helm}/{chartName}/values.yaml {valueOverrides} {KubeNodeFolder.Helm}/{chartName}")
                        .EnsureSuccess();

                    try
                    {
                        NeonHelper.WaitFor(
                            () =>
                            {
                                var response = SudoCommand($"helm status {releaseName} --namespace {@namespace}")
                                    .EnsureSuccess();

                                return response.OutputText.Contains("STATUS: deployed");
                            },
                            timeout:           TimeSpan.FromSeconds(300),
                            pollInterval:      TimeSpan.FromSeconds(1),
                            cancellationToken: controller.CancellationToken);
                    }
                    catch (TimeoutException e)
                    {
                        controller.LogProgressError($"Failed to install helm chart: {@namespace}/{releaseName}");
                        controller.LogProgressError(e.Message);

                        var status = SudoCommand($"helm status {releaseName} --namespace {@namespace} --show-desc")
                            .EnsureSuccess();

                        controller.LogProgressError(status.AllText);
                        throw;
                    }
                });
        }

        /// <summary>
        /// Fixes any problems with the Kubernetes static pod manifests.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public void UpdateKubernetesStaticManifests(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var clusterDefinition = cluster.SetupState.ClusterDefinition;

            controller.LogProgress(this, verb: "fix", message: "kubernetes static pod manifests");

            try
            {
                // $hack(jefflill):
                //
                // We need to remove the [--pod-eviction-timeout] line in the [kube-controller-manager] manifest
                // file.  [kubeadm init] generated the manifest file with this option but [kube-controller-manager]
                // complains that this is an unknown option.

                var manifestText = DownloadText("/etc/kubernetes/manifests/kube-controller-manager.yaml");
                var sb           = new StringBuilder();

                foreach (var line in new StringReader(manifestText).Lines())
                {
                    if (!line.Contains("--pod-eviction-timeout"))
                    {
                        sb.AppendLineLinux(line);
                    }
                }

                UploadText("/etc/kubernetes/manifests/kube-controller-manager.yaml", sb.ToString(), permissions: "600", owner: "root");

                // $hack(jefflill):
                //
                // For control-plane nodes, We need to update the API Server manifest's [--enable-admission-plugins]
                // line the to include the default [NodeRestriction] plugin as well as the other plugins required
                // for NEONKUBE SSO.
                //
                // WE also need to search for a [--feature-gates] command line argument.  If one is present,
                // we'll replace it, otherwise we'll append a new one with any feature gates enabled in the
                // cluster definition.
                //
                // Finally, we're going to add the [--v=LOG_LEVEL] option specifying the log level from the
                // cluster definition and we're going to set the [GOGC=25] environment variable to enable
                // more aggressive GC.

                if (this.Role == NodeRole.ControlPlane)
                {
                    manifestText = DownloadText("/etc/kubernetes/manifests/kube-apiserver.yaml");

                    // Update the features gates.

                    sb.Clear();
                    foreach (var line in new StringReader(manifestText).Lines())
                    {
                        if (!line.Contains("--enable-admission-plugins"))
                        {
                            sb.AppendLineLinux($"{line},NamespaceLifecycle,LimitRanger,ServiceAccount,DefaultStorageClass,DefaultTolerationSeconds,MutatingAdmissionWebhook,ValidatingAdmissionWebhook,Priority,ResourceQuota");
                        }
                    }

                    var manifest   = NeonHelper.YamlDeserialize<dynamic>(manifestText);
                    var spec       = manifest["spec"];
                    var containers = spec["containers"];
                    var container  = containers[0];
                    var command    = (List<object>)container["command"];
                    var sbFeatures = new StringBuilder();

                    foreach (var featureGate in clusterDefinition.Kubernetes.FeatureGates
                        .Where(featureGate => KubeHelper.IsValidFeatureGate(featureGate.Key)))
                    {
                        sbFeatures.AppendWithSeparator($"{featureGate.Key}={NeonHelper.ToBoolString(featureGate.Value)}", ",");
                    }

                    if (sbFeatures.Length > 0)
                    {
                        var featureGateOption = $"--feature-gates={sbFeatures}";
                        var existingArgIndex  = -1;

                        for (int i = 0; i < command.Count; i++)
                        {
                            var arg = (string)command[i];

                            if (arg.StartsWith("--feature-gates="))
                            {
                                existingArgIndex = i;
                                break;
                            }
                        }

                        if (existingArgIndex >= 0)
                        {
                            command[existingArgIndex] = featureGateOption;
                        }
                        else
                        {
                            command.Add(featureGateOption);
                        }
                    }

                    // Add the GOGC environment variable.

                    var env = (List<Dictionary<string, string>>)null;

                    foreach (var item in container)
                    {
                        if (item.Key == "env")
                        {
                            env = (List<Dictionary<string, string>>)item.Value;
                            break;
                        }
                    }

                    if (env == null)
                    {
                        container.Add("env", env = new List<Dictionary<string, string>>());
                    }

                    env.Add(
                        new Dictionary<string, string>()
                        {
                            { "name", "GOGC" },
                            { "value", "25" }
                        });

                    // $hack(jefflill):
                    //
                    // For some reason, the API server fails when the [GOGC] environment variable
                    // value is not surrounded by double quotes which [NeonHelper.YamlSerialize()]
                    // doesn't do for dynamic properties.  We could deserailize the manifest into
                    // a proper type and use the:
                    //
                    //      [YamlMember(ScalarStyle = ScalarStyle.DoubleQuoted)]
                    //
                    // attribute but I don't want to mess with that right now.
                    //
                    // We're going to fix this with code that adds double quotes to any [env] values
                    // that don't already have them.  This is a bit fragile because it assumes that
                    // there's a maximum of only one "env:" property section in the manifest.

                    var manifestYaml      = (string)NeonHelper.YamlSerialize(manifest);
                    var manifestYamlLines = manifestYaml.ToLines().ToArray();
                    var envLineIndex      = -1;

                    for (int i = 0; i < manifestYamlLines.Length; i++)
                    {
                        if (manifestYamlLines[i].Trim().StartsWith("env:"))
                        {
                            envLineIndex = i;
                            break;
                        }
                    }

                    if (envLineIndex >= 0)
                    {
                        var i = envLineIndex + 1;

                        while (i + 1 < manifestYamlLines.Length)
                        {
                            // The [i] and [1+1] lines are expected to be the name/value properties.

                            if (!manifestYamlLines[i].Trim().StartsWith("- name:") ||
                                !manifestYamlLines[i + 1].Trim().StartsWith("value:"))
                            {
                                break;
                            }

                            var valueLine = manifestYamlLines[i + 1];
                            var colonPos  = valueLine.IndexOf(':');
                            var value     = valueLine.Substring(colonPos + 1).Trim();

                            if (!value.StartsWith("\"") && !value.EndsWith("\""))
                            {
                                valueLine = $"{valueLine.Substring(0, colonPos)}: \"{value}\"";
                                manifestYamlLines[i + 1] = valueLine;
                            }

                            i += 2;
                        }

                        var sbManifest = new StringBuilder();

                        foreach (var line in manifestYamlLines)
                        {
                            sbManifest.AppendLineLinux(line);
                        }

                        manifestYaml = sbManifest.ToString();
                    }

                    // Upload the manifest; the pod will restart automatically.

                    UploadText("/etc/kubernetes/manifests/kube-apiserver.yaml", manifestYaml, permissions: "600", owner: "root");
                }

            }
            catch (Exception e)
            {
                controller.LogProgressError(NeonHelper.ExceptionError(e));
            }
        }
    }
}
