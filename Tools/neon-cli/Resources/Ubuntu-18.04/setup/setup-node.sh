#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-node.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script continues the configuration of a node VM by assigning its
# hostname and adding it to a Docker cluster.
#
# Note: This should be called after the node has been initialized via
#       a direct call to [setup-prep-node.sh] or after it has been
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

# Load the hive configuration and setup utilities.

. $<load-cluster-conf>
. setup-utility.sh

# Verify that the node has been prepared.

if [ ! -f ${NEON_STATE_FOLDER}/setup/prepared ] ; then
    echo "*** ERROR: This node has not been prepared." 1>&2
    exit 1
fi

# Ensure that setup is idempotent.

startsetup node

# Remove unncessary home folders that might be present on older prepped OS images.

if [ -d ${HOME}/.bundles ] ; then
    rm -r ${HOME}/.bundles
fi

# Install some common packages:
#
#	jq			        JSON parser (useful for shell scripts)
#	aptitude	        Apt related utilities
#	gdebi-core	        Installs .deb package files AND their dependencies
#   mmv                 Easy multiple file renaming
#   ca-certificates     Latest certificate authority certs

safe-apt-get update
safe-apt-get install -yq jq aptitude gdebi-core mmv ca-certificates

# Configure the hostname.
#
# NOTE: This assumes that the hostname was originally configured 
#       configured to be "ubuntu" as described in:
#
#       [Ubuntu-18.04 cluster Template.docx]

hostname ${NEON_NODE_NAME}
echo ${NEON_NODE_NAME} > /etc/hostname
sed -i "s/ubuntu/${NEON_NODE_NAME}/g" /etc/hosts

# All Neon servers will be configured for UTC time.

timedatectl set-timezone UTC

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

# $todo(jeff.lill):
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
DefaultLimitNOFILE=infinity
DefaultLimitNPROC=infinity
DefaultLimitMEMLOCK=infinity
EOF

chmod 664 /etc/systemd/user.conf.d/50-neon.conf

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
#

# Uncomment the next two lines to enable Spoof protection (reverse-path filter)
# Turn on Source Address Verification in all interfaces to
# prevent some spoofing attacks
#net.ipv4.conf.default.rp_filter=1
#net.ipv4.conf.all.rp_filter=1

# Uncomment the next line to enable TCP/IP SYN cookies
# See http://lwn.net/Articles/277146/
# Note: This may impact IPv6 TCP sessions too
#net.ipv4.tcp_syncookies=1

# Uncomment the next line to enable packet forwarding for IPv4
#net.ipv4.ip_forward=1

# Uncomment the next line to enable packet forwarding for IPv6
#  Enabling this option disables Stateless Address Autoconfiguration
#  based on Router Advertisements for this host
#net.ipv6.conf.all.forwarding=1


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
#kernel.sysrq=1

###################################################################
# Protected links
#
# Protects against creating or following links under certain conditions
# Debian kernels have both set to 1 (restricted) 
# See https://www.kernel.org/doc/Documentation/sysctl/fs.txt
#fs.protected_hardlinks=0
#fs.protected_symlinks=0

###################################################################
# TWEAK: neonKUBE settings:

# Explicitly set the maximum number of file descriptors for the
# entire system.  This looks like it defaults to [398327] for
# Ubuntu 16.04 so we're going to pin this value to enforce
# consistency across Linux updates, etc.

fs.file-max=398327

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

# Randomize addresses of mmap base, heap, stack and VDSO page
kernel.randomize_va_space = 2

# Provide protection from ToCToU races
fs.protected_hardlinks=1

# Provide protection from ToCToU races
fs.protected_symlinks=1

# Make locating kernel addresses more difficult
kernel.kptr_restrict=1

# Set ptrace protections
kernel.yama.ptrace_scope=1

# Set perf only available to root
kernel.perf_event_paranoid=2
EOF

#------------------------------------------------------------------------------
# Edit [/etc/sysctl.conf] to boost the number of RAM pages a process can map
# as well as increasing the number of available source ephemeral TCP ports,
# pending connection backlog, packet receive queue size.

cat <<EOF >> /etc/sysctl.conf

# Allow processes to lock up to 64GB worth of 4K pages into RAM.
vm.max_map_count = 16777216

# Set the network packet receive queue.
net.core.netdev_max_backlog = 2000

# Specify the range of TCP ports that can be used by client sockets.
net.ipv4.ip_local_port_range = 9000 65535

# Set the pending TCP connection backlog.
net.core.somaxconn = 25000
net.ipv4.tcp_max_syn_backlog = 25000
EOF

#------------------------------------------------------------------------------
# iptables may be configured to track only a small number of TCP connections by
# default.  We're going to explicitly set the limit to 1 million connections.
# This will consume about 8MiB of RAM (so not too bad).

cat <<EOF > /etc/modprobe.d/nf_conntrack.conf
# Explicitly set the maximum number of TCP connections that iptables can track.
# Note that this number is multiplied by 8 to obtain the connection count.
options nf_conntrack hashsize=125000
EOF

#------------------------------------------------------------------------------
# Databases are generally not compatible with transparent huge pages.  It appears
# that the best way to disable this is with a simple service.

cat <<EOF > /lib/systemd/system/neonkube-disable-thp.service
# Disables transparent home pages.

[Unit]
Description=Disable transparent home pages (THP)

[Service]
Type=simple
ExecStart=/bin/sh -c "echo 'never' > /sys/kernel/mm/transparent_hugepage/enabled && echo 'never' > /sys/kernel/mm/transparent_hugepage/defrag"

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neonkube-disable-thp
systemctl daemon-reload
systemctl restart neonkube-disable-thp

#------------------------------------------------------------------------------
# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].  This will allow us to easily capture these logs in The
# future so they can be included in a cluster logging solution.
#
# We're also setting [MaxRetentionSec=345600] which limits log local retention 
# to 4 days.  This overrides the default policy which will consume up to 10%
# of the local file system while still providing enough time for operators
# to manually review local logs when something bad happened to hive logging.

cat <<EOF >> /etc/systemd/journald.conf
#------------------------------------------------------------------------------
# FILE:         journald.conf
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].
#
# We're also setting [MaxRetentionSec=86400] which limits log local retention 
# to one day.  This overrides the default policy which will consume up to 10%
# of the local file system while still providing enough time for operators
# to manually review local logs when something bad happened to hive logging.
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
# on the host node.

# $todo(jeff.lill):
#
# The [SshProxy] cleaner assumes that nobody is going to have SshProxy commands
# that run for more than one day (which is pretty likely).  A better approach
# would be to look for temporary command folders THAT HAVE COMPLETED (e.g. HAVE
# an [exit] code file) and are older than one day (or perhaps even older than an
# hour or two) and then purge those.  Not a high priority.

cat <<EOF > ${NEON_BIN_FOLDER}/neonkube-cleaner
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neonkube-cleaner
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# This is a simple service script that periodically cleans accumulated files
# on the host node including:
#
#   1. Shred and delete the root account's [.bash-history] file 
#      as a security measure.  These commands could include
#      sensitive information such as credentials, etc.
#
#   2. Purge temporary Neon command files uploaded by SshProxy.  These
#      are located within folder beneath [/dev/shm/neonkube/cmd].  Although
#      SshProxy removes these files after commands finish executing, it
#      is possible to see these accumulate if the session was interrupted.
#      We'll purge folders and files older than one day.
#
$   3. Clean the temporary file SshProxy upload and execute folders.

history_path1=${HOME}/.bash_history
history_path2=/root/.bash_history
sleep_seconds=300

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

    # Clean the [SshProxy] temporary command files.

    if [ -d /dev/shm/neonkube/cmd ] ; then
        echo "[INFO] Cleaning: /dev/shm/neonkube/cmd"
        find /dev/shm/neonkube/cmd ! -name . -type d -mtime +0 -exec rm -rf {} \; -prune
    fi

    # Clean the [SshProxy] temporary upload files.

    if [ -d "${HOME}/.upload" ] ; then
        echo "[INFO] Cleaning: ${HOME}/.upload"
        find "${HOME}/.upload" ! -name . -type d -mtime +0 -exec rm -rf {} \; -prune
    fi

    # Clean the [SshProxy] temporary download files.

    if [ -d "${HOME}/.download" ] ; then
        echo "[INFO] Cleaning: ${HOME}/.download"
        find "${HOME}/.download" ! -name . -type d -mtime +0 -exec rm -rf {} \; -prune
    fi

    # Clean the [SshProxy] temporary exec files.

    if [ -d "${HOME}/.exec" ] ; then
        echo "[INFO] Cleaning: "${HOME}/.exec""
        find "${HOME}/.exec" ! -name . -type d -mtime +0 -exec rm -rf {} \; -prune
    fi

    # Sleep for a while before trying again.

    sleep \${sleep_seconds}
done
EOF

chmod 700 ${NEON_BIN_FOLDER}/neonkube-cleaner

# Generate the [neonkube-cleaner] systemd unit.

cat <<EOF > /lib/systemd/system/neonkube-cleaner.service
# A service that periodically shreds the root's Bash history
# as a security measure.

[Unit]
Description=neonkube-cleaner
Documentation=
After=local-fs.target
Requires=local-fs.target

[Service]
ExecStart=${NEON_BIN_FOLDER}/neonkube-cleaner
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neonkube-cleaner
systemctl daemon-reload
systemctl restart neonkube-cleaner

#------------------------------------------------------------------------------
# Configure the PowerDNS Recursor.

curl -4fsSLv ${CURL_RETRY} $<net.powerdns.recursor.package.uri> -o /tmp/pdns-recursor.deb
gdebi --non-interactive /tmp/pdns-recursor.deb
rm /tmp/pdns-recursor.deb
systemctl stop pdns-recursor

# Backup the configuration file installed by the package.

cp /etc/powerdns/recursor.conf /etc/powerdns/recursor.conf.backup

# Generate [/etc/powerdns/hosts] file that the PowerDNS Recursor
# will use to authoritatively answer local cluster questions.  Note
# that the $<net.powerdns.recursor.hosts> macro is initialized to 
# the host entries customized for this specific node.

cat <<EOF > /etc/powerdns/hosts
$<net.powerdns.recursor.hosts>
EOF

# Generate the custom local settings.

cat <<EOF > /etc/powerdns/recursor.conf
###############################################################################
# cluster custom PowerDNS Recursor Server configuration.

#################################
# Allow requests only from well-known Internet private subnets as well as
# the local loopback interfaces.
allow-from=10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8

#################################
# allow-from-file       If set, load allowed netmasks from this file
#
# allow-from-file=

#################################
# any-to-tcp    Answer ANY queries with tc=1, shunting to TCP
#
# any-to-tcp=no

#################################
# api-config-dir        Directory where REST API stores config and zones
#
# api-config-dir=

#################################
# api-key       Static pre-shared authentication key for access to the REST API
#
# api-key=

#################################
# api-logfile   Location of the server logfile (used by the REST API)
#
# api-logfile=/var/log/pdns.log

#################################
# api-readonly  Disallow data modification through the REST API when set
#
# api-readonly=no

#################################
# auth-zones    Zones for which we have authoritative data, comma separated domain=file pairs
#
# auth-zones=

#################################
# carbon-interval       Number of seconds between carbon (graphite) updates
#
# carbon-interval=30

#################################
# carbon-ourname        If set, overrides our reported hostname for carbon stats
#
# carbon-ourname=

#################################
# carbon-server If set, send metrics in carbon (graphite) format to this server IP address
#
# carbon-server=

#################################
# chroot        switch to chroot jail
#
# chroot=

#################################
# client-tcp-timeout    Timeout in seconds when talking to TCP clients
#
# client-tcp-timeout=2

#################################
# config-dir    Location of configuration directory (recursor.conf)
#
config-dir=/etc/powerdns

#################################
# config-name   Name of this virtual configuration - will rename the binary image
#
# config-name=

#################################
# cpu-map       Thread to CPU mapping, space separated thread-id=cpu1,cpu2..cpuN pairs
#
# cpu-map=

#################################
# daemon        Operate as a daemon
#
# daemon=no

#################################
# delegation-only       Which domains we only accept delegations from
#
# delegation-only=

#################################
# disable-packetcache   Disable packetcache
#
# disable-packetcache=no

#################################
# disable-syslog        Disable logging to syslog.  We don't need this because we're capturing
#                       systemd logs.
#
disable-syslog=yes

#################################
# dnssec        DNSSEC mode: off/process-no-validate (default)/process/log-fail/validate
#
# dnssec=process-no-validate

#################################
# dnssec-log-bogus      Log DNSSEC bogus validations
#
# dnssec-log-bogus=no

#################################
# dont-query    If set, do not query these netmasks for DNS data
#
# dont-query=127.0.0.0/8, 10.0.0.0/8, 100.64.0.0/10, 169.254.0.0/16, 192.168.0.0/16, 172.16.0.0/12, ::1/128, fc00::/7, fe80::/10, 0.0.0.0/8, 192.0.0.0/24, 192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24, 240.0.0.0/4, ::/96, ::ffff:0:0/96, 100::/64, 2001:db8::/32

#################################
# ecs-ipv4-bits Number of bits of IPv4 address to pass for EDNS Client Subnet
#
# ecs-ipv4-bits=24

#################################
# ecs-ipv6-bits Number of bits of IPv6 address to pass for EDNS Client Subnet
#
# ecs-ipv6-bits=56

#################################
# ecs-scope-zero-address        Address to send to whitelisted authoritative servers for incoming queries with ECS prefix-length source of 0
#
# ecs-scope-zero-address=

#################################
# edns-outgoing-bufsize Outgoing EDNS buffer size
#
# edns-outgoing-bufsize=1680

#################################
# edns-subnet-whitelist List of netmasks and domains that we should enable EDNS subnet for
#
# edns-subnet-whitelist=

#################################
# entropy-source        If set, read entropy from this file
#
# entropy-source=/dev/urandom

################################
# Authoritatively answer from the recursor [/etc/powerdns/hosts] file.
#
etc-hosts-file=/etc/powerdns/hosts

#################################
# export-etc-hosts      If we should serve up contents from [/etc/powerdns/hosts]
#
export-etc-hosts=yes

#################################
# export-etc-hosts-search-suffix        Also serve up the contents of /etc/hosts with this suffix
#
# export-etc-hosts-search-suffix=

#################################
# forward-zones Zones for which we forward queries, comma separated domain=ip pairs
#
# forward-zones=

#################################
# forward-zones-file    File with (+)domain=ip pairs for forwarding
#
# forward-zones-file=

#################################
# Configure recursion to the upstream DNS servers.
#
forward-zones-recurse=.=$<net.nameservers>

#################################
# gettag-needs-edns-options     If EDNS Options should be extracted before calling the gettag() hook
#
# gettag-needs-edns-options=no

#################################
# hint-file     If set, load root hints from this file
#
# hint-file=

#################################
# include-dir   Include *.conf files from this directory
#
# include-dir=

#################################
# latency-statistic-size        Number of latency values to calculate the qa-latency average
#
# latency-statistic-size=10000

#################################
# Bind to all network interfaces.
#
local-address=0.0.0.0

#################################
# local-port    port to listen on
#
# local-port=53

#################################
# log-common-errors     If we should log rather common errors
#
# log-common-errors=no

#################################
# log-rpz-changes       Log additions and removals to RPZ zones at Info level
#
# log-rpz-changes=no

#################################
# log-timestamp Print timestamps in log lines, useful to disable when running with a tool that timestamps stdout already
#
# log-timestamp=yes

#################################
# logging-facility      Facility to log messages as. 0 corresponds to local0
#
# logging-facility=

#################################
# lowercase-outgoing    Force outgoing questions to lowercase
#
# lowercase-outgoing=no

#################################
# lua-config-file       More powerful configuration options
#
# lua-config-file=

#################################
# lua-dns-script        Filename containing an optional 'lua' script that will be used to modify dns answers
#
# lua-dns-script=

#################################
# max-cache-entries     If set, maximum number of entries in the main cache
#
# max-cache-entries=1000000

#################################
# Cache responses for a maximum of 30 seconds (potentially overriding
# the TTL returned in the original answer).
#
max-cache-ttl=30

#################################
# max-mthreads  Maximum number of simultaneous Mtasker threads
#
# max-mthreads=2048

#################################
# max-negative-ttl	maximum number of seconds to keep a negative cached entry in memory
#
max-negative-ttl=30

#################################
# max-packetcache-entries       maximum number of entries to keep in the packetcache
#
# max-packetcache-entries=500000

#################################
# max-qperq     Maximum outgoing queries per query
#
# max-qperq=50

#################################
# max-recursion-depth   Maximum number of internal recursion calls per query, 0 for unlimited
#
# max-recursion-depth=40

#################################
# max-tcp-clients       Maximum number of simultaneous TCP clients
#
# max-tcp-clients=128

#################################
# max-tcp-per-client    If set, maximum number of TCP sessions per client (IP address)
#
# max-tcp-per-client=0

#################################
# max-tcp-queries-per-connection        If set, maximum number of TCP queries in a TCP connection
#
# max-tcp-queries-per-connection=0

#################################
# max-total-msec        Maximum total wall-clock time per query in milliseconds, 0 for unlimited
#
# max-total-msec=7000

#################################
# minimum-ttl-override  Set under adverse conditions, a minimum TTL
#
# minimum-ttl-override=0

#################################
# network-timeout       Wait this number of milliseconds for network i/o
#
# network-timeout=1500

#################################
# no-shuffle    Don't change
#
# no-shuffle=off

#################################
# non-local-bind        Enable binding to non-local addresses by using FREEBIND / BINDANY socket options
#
# non-local-bind=no

#################################
# nsec3-max-iterations  Maximum number of iterations allowed for an NSEC3 record
#
# nsec3-max-iterations=2500

#################################
# packetcache-servfail-ttl      maximum number of seconds to keep a cached servfail entry in packetcache
#
# packetcache-servfail-ttl=60

#################################
# packetcache-ttl       maximum number of seconds to keep a cached entry in packetcache
#
# packetcache-ttl=3600

#################################
# pdns-distributes-queries      If PowerDNS itself should distribute queries over threads
#
# pdns-distributes-queries=yes

#################################
# processes     Launch this number of processes (EXPERIMENTAL, DO NOT CHANGE)
#
# processes=1

#################################
# query-local-address   Source IP address for sending queries
#
# query-local-address=0.0.0.0

#################################
# query-local-address6  Source IPv6 address for sending queries. IF UNSET, IPv6 WILL NOT BE USED FOR OUTGOING QUERIES
#
# query-local-address6=

#################################
# reuseport     Enable SO_REUSEPORT allowing multiple recursors processes to listen to 1 address
#
# reuseport=no

#################################
# root-nx-trust If set, believe that an NXDOMAIN from the root means the TLD does not exist
#
# root-nx-trust=yes

#################################
# security-poll-suffix  Domain name from which to query security update notifications
#
# security-poll-suffix=secpoll.powerdns.com.

#################################
# serve-rfc1918 If we should be authoritative for RFC 1918 private IP space
#
# serve-rfc1918=yes

#################################
# server-down-max-fails Maximum number of consecutive timeouts (and unreachables) to mark a server as down ( 0 => disabled )
#
# server-down-max-fails=64

#################################
# server-down-throttle-time     Number of seconds to throttle all queries to a server after being marked as down
#
# server-down-throttle-time=60

#################################
# server-id     Returned when queried for 'id.server' TXT or NSID, defaults to hostname
#
# server-id=

# ---------------------------------------------------------
# todo(jeff.lill):
#
# I'm temporarily disabling having the service change it's identity to
# [pdns:pdns] after it starts due to permissions issues when I use
# [rec_control reload-zones] to re-read the hosts file.
#
# This introduces a potential security issue, that is somewhat mitigated
# by the fact that we're allowing only queries from private subnets.
# 
# This should be investigated and corrected at some point.  The tracking
# issue is:
#
#     https://github.com/jefflill/NeonForge/issues/211
#
# ---------------------------------------------------------

#################################
# setgid        If set, change group id to this gid for more security
#
setgid=pdns

#################################
# setuid        If set, change user id to this uid for more security
#
setuid=pdns

#################################
# single-socket If set, only use a single socket for outgoing queries
#
# single-socket=off

#################################
# snmp-agent    If set, register as an SNMP agent
#
# snmp-agent=no

#################################
# snmp-master-socket    If set and snmp-agent is set, the socket to use to register to the SNMP master
#
# snmp-master-socket=

#################################
# soa-minimum-ttl       Don't change
#
# soa-minimum-ttl=0

#################################
# socket-dir    Where the controlsocket will live, /var/run when unset and not chrooted
#
# socket-dir=

#################################
# socket-group  Group of socket
#
# socket-group=

#################################
# socket-mode   Permissions for socket
#
# socket-mode=

#################################
# socket-owner  Owner of socket
#
# socket-owner=

#################################
# spoof-nearmiss-max    If non-zero, assume spoofing after this many near misses
#
# spoof-nearmiss-max=20

#################################
# stack-size    stack size per mthread
#
# stack-size=200000

#################################
# statistics-interval   Number of seconds between printing of recursor statistics, 0 to disable
#
# statistics-interval=1800

#################################
# stats-ringbuffer-entries      maximum number of packets to store statistics for
#
# stats-ringbuffer-entries=10000

#################################
# tcp-fast-open Enable TCP Fast Open support on the listening sockets, using the supplied numerical value as the queue size
#
# tcp-fast-open=0

#################################
# threads       Launch this number of threads
#
# threads=2

#################################
# trace if we should output heaps of logging. set to 'fail' to only log failing domains
#
# trace=off

#################################
# udp-truncation-threshold      Maximum UDP response size before we truncate
#
# udp-truncation-threshold=1680

#################################
# use-incoming-edns-subnet      Pass along received EDNS Client Subnet information
#
# use-incoming-edns-subnet=no

#################################
# version-string        string reported on version.pdns or version.bind
#
# version-string=PowerDNS Recursor 4.1.1 (built Jan 22 2018 13:40:51 by root@f8e88a9fd7c0)

#################################
# webserver     Start a webserver (for REST API)
#
# webserver=no

#################################
# webserver-address     IP Address of webserver to listen on
#
# webserver-address=127.0.0.1

#################################
# webserver-allow-from  Webserver access is only allowed from these subnets
#
# webserver-allow-from=127.0.0.1,::1

#################################
# webserver-password    Password required for accessing the webserver
#
# webserver-password=

#################################
# webserver-port        Port of webserver to listen on
#
# webserver-port=8082

#################################
# write-pid     Write a PID file
#
# write-pid=yes

#################################
# WARNING: Be sure to comment these out for production clusters.
#
# Debugging related settings.  Be sure to comment these out for production clusters.
#
# loglevel      Amount of logging. Higher is more. Do not set below 3
# quiet         Suppress logging of questions and answers
#
# loglevel=9
# quiet=no
EOF

# Set PowerDNS related config file permissions.

chown -R root:root /etc/powerdns
chmod -R 774 /etc/powerdns

# Start the PowerDNS Recursor.

systemctl start pdns-recursor
sleep 10    # Give the service some time to start.

# Configure the local DNS resolver to override any DHCP or other interface
# specific settings and just query the PowerDNS Recursor running locally
# on this host node.

echo "" > /etc/resolvconf/interface-order
echo "nameserver ${NEON_NODE_IP}" > /etc/resolvconf/resolv.conf.d/base
resolvconf -u

# Indicate that the script has completed.

endsetup node
