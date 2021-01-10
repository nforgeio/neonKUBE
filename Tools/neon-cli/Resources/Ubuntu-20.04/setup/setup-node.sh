#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-node.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script continues the configuration of a node VM by assigning its
# hostname and adding it to a Docker cluster.
#
# Note: This should be called after the node has been initialized via
#       a direct call to [setup-prep.sh] or after it has been
#       cloned from another initialized node.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-NODE                               **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Verify that the node has been prepared.

if [ ! -f ${NEON_STATE_FOLDER}/setup/prepared ] ; then
    echo "*** ERROR: This node has not been prepared." 1>&2
    exit 1
fi

# Ensure that setup is idempotent.

startsetup node

#------------------------------------------------------------------------------
# Remove the [neon-init] service.  This is no longer required after it
# runs once (first boot) during node provisioning configures the network and 
# and services.

if [ -f /etc/systemd/system/neon-init.service ]; then
    echo "** Remove: neon-init.service"
    rm -f /etc/systemd/system/neon-init.service
    rm -f ${NEON_BIN_FOLDER}/neon-init.sh
fi

# Ensure that the home directory for the [temp] user prepare created
# to relocate the [sysadmin] user ID is deleted.  Older builds of the
# [neon prepare node-template] command didn't delete this.

rm -rf /home/temp

# Install some common packages:
#
#   nano                Text editor
#   sysstat             Linux monitoring tools
#   dstat               Linux performance monitoring
#   iotop               Linux I/O monitoring
#   apache2-utils       Apache utilities
#   daemon              daemon wrapper
#	jq			        JSON parser (useful for shell scripts)
#	aptitude	        Apt related utilities
#	gdebi-core	        Installs .deb package files AND their dependencies
#   mmv                 Easy multiple file renaming
#   ca-certificates     Latest certificate authority certs

safe-apt-get update
safe-apt-get install -yq --allow-downgrades nano sysstat dstat iotop iptraf apache2-utils daemon jq aptitude gdebi-core mmv ca-certificates nfs-common

# All Neon servers will be configured for UTC time.

timedatectl set-timezone UTC

# We need to blacklist the floppy drive.  Not doing this can cause
# node failures:

rmmod floppy
echo "blacklist floppy" | tee /etc/modprobe.d/blacklist-floppy.conf
dpkg-reconfigure initramfs-tools

# Enable system statistics collection (e.g. Page Faults,...)

sed -i '/^ENABLED="false"/c\ENABLED="true"' /etc/default/sysstat

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
# to Kubernetes, it probable makes more sense to set limits for the fewer
# specific services we're actually deploying.

cat <<EOF > /etc/security/limits.conf
# /etc/security/limits.conf
#
#Each line describes a limit for a user in the form:
#
#<domain>        <type>  <item>  <value>
#
#Where:
#<domain> can be:
#        - a user name
#        - a group name, with @group syntax
#        - the wildcard *, for default entry
#        - the wildcard %, can be also used with %group syntax,
#                 for maxlogin limit
#        - NOTE: group and wildcard limits are not applied to root.
#          To apply a limit to the root user, <domain> must be
#          the literal username root.
#
#<type> can have the two values:
#        - "soft" for enforcing the soft limits
#        - "hard" for enforcing hard limits
#
#<item> can be one of the following:
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
# [systemd] has its own configuration limits configuration files and ignores
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

#------------------------------------------------------------------------------
# Tweak some kernel settings.  I extracted this file from a clean Ubuntu 18.04
# installed and then made the changes marked by the "# TWEAK" comment.

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
net.ipv4.ip_forward = 0

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

echo nf_conntrack > /etc/modules

#------------------------------------------------------------------------------
# Databases are generally not compatible with transparent huge pages.  It appears
# that the best way to disable this is with a simple service.

cat <<EOF > /lib/systemd/system/neon-disable-thp.service
# Disables transparent home pages.

[Unit]
Description=Disable transparent home pages (THP)

[Service]
Type=simple
ExecStart=/bin/sh -c "echo 'never' > /sys/kernel/mm/transparent_hugepage/enabled && echo 'never' > /sys/kernel/mm/transparent_hugepage/defrag"

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

cat <<EOF >> /etc/systemd/journald.conf
#------------------------------------------------------------------------------
# FILE:         journald.conf
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
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
# hour or two) and then purge those.  Not a high priority.

cat <<EOF > ${NEON_BIN_FOLDER}/neon-cleaner
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-cleaner
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
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

history_path1=${HOME}/.bash_history
history_path2=/root/.bash_history
sleep_seconds=3600

echo "[INFO] Starting: [sleep_time=\${sleep_seconds} seconds]"

while true
do
    # Clean [.bash-history]

    if [ -f \${history_path1} ] ; then
        echo "[INFO] Shredding [\${history_path1}]"
        result=\$(shred -uz \${history_path1})
        if [ "\$?" != "0" ] ; then
            echo "[WARN] \${result}"
        fi
    fi

    if [ -f \${history_path2} ] ; then
        echo "[INFO] Shredding [\${history_path2}]"
        result=\$(shred -uz \${history_path2})
        if [ "\$?" != "0" ] ; then
            echo "[WARN] \${result}"
        fi
    fi

    # Clean the [LinuxSshProxy] temporary download files.

    if [ -d "$/home/sysadmin/.neon/download" ] ; then
        echo "[INFO] Cleaning: /home/sysadmin/.neon/download"
        find "/home/sysadmin/.neon/download/*" -type d -ctime +1 | xargs rm -rf
    fi

    # Clean the [LinuxSshProxy] temporary exec files.

    if [ -d "/home/sysadmin/.neon/exec" ] ; then
        echo "[INFO] Cleaning: "/home/sysadmin/.neon/exec""
        find "/home/sysadmin/.neonklube/exec/*" -type d -ctime +1 | xargs rm -rf
    fi

    if [ -d "/home/sysadmin/.neon/upload" ] ; then
        echo "[INFO] Cleaning: /home/sysadmin/.neon/upload"
        find "/home/sysadmin/.neon/upload/*" -type d -ctime +1 | xargs rm -rf
    fi

    # Sleep for a while before trying again.

    sleep \${sleep_seconds}
done
EOF

chmod 700 ${NEON_BIN_FOLDER}/neon-cleaner

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
ExecStart=${NEON_BIN_FOLDER}/neon-cleaner
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neon-cleaner
systemctl daemon-reload
systemctl restart neon-cleaner

systemctl enable iscsid
systemctl daemon-reload
systemctl restart iscsid

# Indicate that the script has completed.

endsetup node
