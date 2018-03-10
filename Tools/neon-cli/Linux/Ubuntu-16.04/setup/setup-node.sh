#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-node.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script continues the configuration of a node VM by assigning its
# host name and adding it to a Docker cluster.
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

# Load the cluster configuration and setup utilities.

. $<load-cluster-config>
. setup-utility.sh

# Verify that the node has been prepared.

if [ ! -f ${NEON_STATE_FOLDER}/finished-setup-prep-node ] ; then
    echo "*** ERROR: This node has not been prepared." 1>&2
fi

# Ensure that setup is idempotent.

startsetup setup-node

# Remove unncessary home folders that might be present on older prepped OS images.

if [ -d ${HOME}/.bundles ] ; then
    rm -r ${HOME}/.bundles
fi

# Install some common packages:
#
#	jq			JSON parser (useful for shell scripts)
#	aptitude	Apt related utilities
#	gdebi-core	Installs .deb package files AND their dependencies

apt-get update
apt-get install -yq jq aptitude gdebi-core

# Configure the host name.
#
# NOTE: this assumes that the host name was originally configured 
#       configured to be "ubuntu" as described in:
#
#       [Ubuntu-16.04 neonCLUSTER Template.docx]

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

root        soft    nofile  unlimited
root        hard    nofile  unlimited
root        soft    memlock unlimited
root        hard    memlock unlimited

*           soft    nofile  unlimited
*           hard    nofile  unlimited

# End of file
EOF

#------------------------------------------------------------------------------
# Tuning some kernel network settings.

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

# Docker overlay networks require TCP keepalive packets to be
# transmitted at least every 15 minutes on idle connections to
# prevent zombie connections that appear to be alive but don't
# actually transmit data.  This is described here:
#
#	https://github.com/moby/moby/issues/31208
#
# We're going to configure TCP connections to begin sending
# keepalives after 500 minutes of being idle and then every
# 30 seconds thereafter (regardless of any other traffic).
# If keepalives are not ACKed after 2.5 minutes, Linux will
# report the connection as closed to the application layer.

net.ipv4.tcp_keepalive_time = 300
net.ipv4.tcp_keepalive_intvl = 30
net.ipv4.tcp_keepalive_probes = 5

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


###################################################################
# Fluentd/TD-Agent recommended settings:

net.ipv4.tcp_tw_recycle = 1
net.ipv4.tcp_tw_reuse = 1

###################################################################
# OpenVPN requires packet forwarding.

net.ipv4.ip_forward=1
EOF

#--------------------------------------------------------------------------
# We need to modify [/etc/default/grub] to avoid these Docker warnings:
#
#   WARNING: Your kernel does not support cgroup swap limit. 
#   WARNING: Your kernel does not support swap limit capabilities. Limitation discarded.
#
# This is warning that Docker won't be able to accurately measure memory
# and swap file contraints to containers that use old memory stats APIs.
# This can cause big problems, because many apps by default will preallocate
# memory for heaps and such based on the total machine memory and not that
# assigned to the container.  This is common for many Java apps.
#
# This corrects this problem but unfortunately at a cost of 10% of
# performance and 1% memory.  See this for more information:
#
#   https://fabiokung.com/2014/03/13/memory-inside-linux-containers/
#
# We're going to change this line:
#
#       GRUB_CMDLINE_LINUX=""
#
# to:
#
#       GRUB_CMDLINE_LINUX="swapaccount=1" 
#
# NOTE: This will require a reboot to take effect.

sed -i 's!^GRUB_CMDLINE_LINUX=""$!GRUB_CMDLINE_LINUX="memory swapaccount=1"!g' /etc/default/grub
update-grub

#--------------------------------------------------------------------------
# Edit [/etc/sysctl.conf] so that Linux will disable swapping RAM to disk
# when this is requested for this node and then edit [/etc/fstab] to remove
# the swap file and then MASK the [swap.target] to be really sure that
# swapping is disabled.

if ! ${NEON_NODE_SWAP} ; then

    if ! grep neonCLUSTER /etc/sysctl.conf ; then

        cat <<EOF >> /etc/sysctl.conf

###################################################################
# neonCLUSTER settings

# Disable swapping
vm.swappiness = 0
EOF

        # Disable the system swap file (this requires a reboot).

        sed -ir '/swap/d' /etc/fstab
        systemctl mask swap.target
    fi 
fi

#------------------------------------------------------------------------------
# Edit [/etc/sysctl.conf] to boost the number of RAM pages a process can map.

cat <<EOF >> /etc/sysctl.conf

# Allow processes to lock up to 64GB worth of 4K pages into RAM.
vm.max_map_count = 16777216

# Specify the range of TCP ports that can be used by client sockets.
net.ipv4.ip_local_port_range = 9000 65535
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
ExecStart=/bin/sh -c "echo 'never' > /sys/kernel/mm/transparent_hugepage/enabled && echo 'never' > /sys/kernel/mm/transparent_hugepage/defrag"

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neon-disable-thp
systemctl daemon-reload
systemctl restart neon-disable-thp

#------------------------------------------------------------------------------
# Configure a simple service that sets up IPTABLES rules that forward TCP
# packets hitting ports 80 and 443 on any of the [eth#] network interfaces
# to the [neon-proxy-public] on ports 5100 and 5101.
#
# This allows the cluster to handle standard HTTP and HTTPS traffic without
# having to bind to the protected system ports.  This is especially usefuly
# for deployments with brain-dead consumer quality routers that cannot forward
# packets to a different port.

# $todo(jeff.lill):
#
# For now, this is hardcoded for just ports 80 & 443.  Eventually, it might be
# handy to make this a cluster configuration setting or perhaps a [neon-cli]
# command so SMTP or other traffic can also be handled.

# $hack(jeff.lill):
#
# I'm hardcoding the [neon-proxy-public] ports 5100 and 5101 here rather than
# adding a new macro.  Hopefully, these ports will never change again.

cat <<EOF > /usr/local/bin/neon-port-forwarding
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-port-forwarding
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script runs as a systemd service to configure port 80 and 443 port
# forwarding rules.  Note that these rules work because [neon-proxy-public]
# is either running on ever node or we're using the Docker ingress mesh
# network to route these packets to the proxy instances.

echo "[INFO] Configuring port forwarding rules."

# Use [ip link] and [gawk] to list the interfaces that match "eth#"
# and then configure the IPTABLES forwarding rules for those interfaces.

for interface in \$( ip link | gawk '/eth[0..9]+:/ { print gensub(/:/, "", "g", \$2) }' ); do

    # Forward port 80 --> 5100

    echo "[INFO] Forwarding [port 80] packets on [\$interface] to [${NEON_NODE_IP}:5100]."
    iptables -A PREROUTING -t nat -i \$interface -p tcp --dport 80 -j DNAT --to ${NEON_NODE_IP}:5100
    iptables -A FORWARD -p tcp -d ${NEON_NODE_IP} --dport 5100 -j ACCEPT

    # Forward port 443 --> 5101

    echo "[INFO] Forwarding [port 443] packets on [\$interface] to [${NEON_NODE_IP}:5101]."
    iptables -A PREROUTING -t nat -i \$interface -p tcp --dport 443 -j DNAT --to ${NEON_NODE_IP}:5101
    iptables -A FORWARD -p tcp -d ${NEON_NODE_IP} --dport 5101 -j ACCEPT
done

echo "[INFO] Configuration complete."

# We probably don't need to loop here, but it can't hurt.

echo "[INFO] Sleeping."

while true
do
    sleep 3600
done
EOF

chmod 700 /usr/local/bin/neon-port-forwarding

# Generate the [neon-port-forwarding] systemd unit.

cat <<EOF > /lib/systemd/system/neon-port-forwarding.service
# A service that configures the port 80 and 443 port forwarding rules.

[Unit]
Description=neon-port-forwarding
Documentation=
After=wait-for-network.service

[Service]
ExecStart=/usr/local/bin/neon-port-forwarding
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neon-port-forwarding
systemctl daemon-reload
systemctl restart neon-port-forwarding

#------------------------------------------------------------------------------
# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].  We need this so the node's [neon-log-host] container
# will be able to access the journal.
#
# We're also setting [MaxRetentionSec=86400] which limits log local retention 
# to one day.  This overrides the default policy which will consume up to 10%
# of the local file system while still providing enough time for operators
# to manually review local logs when something bad happened to cluster logging.

cat <<EOF >> /etc/systemd/journald.conf
#------------------------------------------------------------------------------
# FILE:         journald.conf
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Configure the systemd journal to perist the journal to the file system at
# [/var/log/journal].  We need this so the node's [neon-log-host] service
# will be able to forward the system logs to the cluster log aggregation
# pipeline.
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
MaxRetentionSec=86400
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
# Install a simple service script that periodically shreds and deletes the 
# the root account's [.bash-history] file as a security measure.

cat <<EOF > /usr/local/bin/neon-security-cleaner
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-security-cleaner
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script runs as a systemd service to periodically shred and remove the
# root account's [.bash_history] file as a security measure.  This will help 
# prevent bad guys from looking for secrets used in Bash by a system operator.

history_path1=${HOME}/.bash_history
history_path2=/root/.bash_history
sleep_seconds=60

echo "[INFO] Starting: [sleep_time=\${sleep_seconds} seconds]"

while true
do
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

    sleep \${sleep_seconds}
done
EOF

chmod 700 /usr/local/bin/neon-security-cleaner

# Generate the [neon-security-cleaner] systemd unit.

cat <<EOF > /lib/systemd/system/neon-security-cleaner.service
# A service that periodically shreds the root's Bash history
# as a security measure.

[Unit]
Description=neon-security-cleaner
Documentation=
After=local-fs.target
Requires=local-fs.target

[Service]
ExecStart=/usr/local/bin/neon-security-cleaner
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

systemctl enable neon-security-cleaner
systemctl daemon-reload
systemctl restart neon-security-cleaner

#------------------------------------------------------------------------------
# Configure the PowerDNS Authoritative Server on the manager nodes if Dynamic
# DNS is enabled for the cluster.

if [ "$<net.dynamicdns.enabled>" ] ; then

	# Download the PowerDNS server.

	curl -4fsSLv ${CURL_RETRY} $<net.powerdns.server.package.uri> -o /tmp/pdns-server.deb
	gdebi --non-interactive /tmp/pdns-server.deb
	rm /tmp/pdns-server.deb

	# Install the PowerDNS REMOTE backend.

	curl -4fsSLv ${CURL_RETRY} $<net.powerdns.backend.remote.package.uri> -o /tmp/pdns-backend-remote.deb
	gdebi --non-interactive /tmp/pdns-backend-remote.deb
	rm /tmp/pdns-backend-remote.deb

	# Stop the service while we configure it and install the recursor.

	systemctl stop pdns

	# Backup the configuration file installed by the package.

	cp /etc/powerdns/pdns.conf /etc/powerdns/pdns.conf.backup

	# Remove any sample local configuration files installed by the package and
	# then add our custom file.

	rm -f /etc/powerdns/bindbackend.conf
	rm -f /etc/powerdns/pdns.d/*

	cat <<EOF > /etc/powerdns/pdns.d/pdns.local.conf
###############################################################################
# neonCLUSTER custom PowerDNS Authoritative Server configuration overrides.

#################################
# local-address		Listen for requests on this interface and port.
# local-ipv6
# local-port	
#
local-address=127.0.0.1
local-ipv6=::1
local-port=$<net.powerdns.port>

#################################
# launch					          Enable the PowerDNS REMOTE backend to use HTTP to
# remote-connection-string	Query the local [neon-dns] listening on the host
#                           network.
#
launch=remote
remote-connection-string=http:url=http://127.0.0.1:$<net.dynamicdns.port>,timeout=2000

#################################
# no-shuffle	Set this to prevent random shuffling of answers.  The [neon-dns]
#             backend service optionally handles this for specific hostnames.
#
no-shuffle=yes

#################################
# negquery-cache-ttl	Seconds to store negative query results in the QueryCache
#
negquery-cache-ttl=5

#################################
# query-cache-ttl		Max seconds to store query results in the QueryCache
#
query-cache-ttl=60

#################################
# disable-syslog		We can disable this because we're capturing the systemd logs.
#
disable-syslog=yes

#################################
# WARNING: Be sure to comment these out for production clusters.
#
# Debugging related settings.
#
# loglevel=5
# log-dns-details=yes
# log-dns-queries=yes
EOF

	# Set PowerDNS related config file permissions.  Note that we're not
	# going to restart the [pdns] service until after we install the recursor
	# to avoid default configuration conflicts.

	chmod -R 775 /etc/powerdns
fi

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
# the host entries for this specific node.

cat <<EOF > /etc/powerdns/hosts
$<net.powerdns.recursor.hosts>
EOF

# Generate the custom local settings and then append them onto the end of the
# default settings installed with by the recursor package.

cat <<EOF > /etc/powerdns/recursor.local.conf
###############################################################################
# neonCLUSTER custom PowerDNS Recursor Server configuration overrides to be
# appended unto the end of the [recursor.conf] file installed by the PowerDNS
# Recursor package.

#################################
# Allow requests only from well-known Internet private subnets as well as
# the local loopback interfaces.
allow-from=10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16, 127.0.0.0/8

################################
# Authoritatively answer from the recursor [hosts] file.
#
etc-hosts-file=/etc/powerdns/hosts
export-etc-hosts=yes

################################
# Configure recursion to the upstream DNS servers.
forward-zones-recurse=.=$<net.nameservers>

#################################
# forward-zones		If the cluster enables Dynamic DNS, we need to specify the 
#                   forward requests for the [*.cluster] and [*.node.cluster] domains
#					to the PowerDNS Authoritative Servers running on the master nodes.
#
forward-zones=cluster=127.0.0.1:$<net.powerdns.port>

#################################
# Bind to all network interfaces.
#
local-address=0.0.0.0

#################################
# Cache responses for a maximum of 5 minutes (potentially overriding
# the TTL returned in the original answer).
#
max-cache-ttl=300

#################################
# max-negative-ttl	maximum number of seconds to keep a negative cached entry in memory
#
# max-negative-ttl=30

#################################
# disable-syslog	Disable logging to syslog, useful when running inside a supervisor that logs stdout
#
disable-syslog=yes

#################################
# WARNING: Be sure to comment these out for production clusters.
#
# Debugging related settings.  Be sure to comment these out for production clusters.
#
# loglevel=6
# quiet=no
EOF

cat /etc/powerdns/recursor.local.conf >> /etc/powerdns/recursor.conf

# Set PowerDNS related config file permissions and then restart the 
# recursor to pick up the new config.

chmod -R 775 /etc/powerdns

# Restart the PowerDNS server and recursor services to pick up the new configs.

if [ "$<net.dynamicdns.enabled>" ] ; then
	systemctl start pdns
fi

systemctl start pdns-recursor
sleep 5		# Give the services some time to start.

# Configure the local DNS resolver to override any DHCP or other interface
# specific settings and just query the PowerDNS Recursor running locally
# on the this host node.

echo "" > /etc/resolvconf/interface-order
echo "nameserver ${NEON_NODE_IP}" > /etc/resolvconf/resolv.conf.d/base
resolvconf -u

#------------------------------------------------------------------------------
# Install Ansible related packages so common playbooks (like Docker related ones)
# will run out-of-the-box.

apt-get install -yq python-pip
pip install docker
pip install PyYAML
pip install jsondiff

#------------------------------------------------------------------------------
# Configure a CRON job that performs daily node maintenance including purging
# unreferenced Docker images.

cat <<EOF > /usr/local/bin/neon-host-maintenance 
#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         neon-host-maintenance
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script runs is invoked by CRON to perform periodic host maintenance incuding
# purging unreferenced Docker images to avoid maxing out the file system.  This
# script needs root privileges.

docker image prune --all --force
EOF

chmod 744 /usr/local/bin/neon-host-maintenance

# $todo(jeff.lill):
#
# It would be nice to log what happened during maintenance and record This
# in Elasticsearch for analysis.

cat <<EOF > /etc/cron.d/neon-host-maintenance
#------------------------------------------------------------------------------
# FILE:         neon-host-maintenance
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Daily neonCLUSTER related host maintenance scheduled for 9:15pm system time (UTC)
# or the middle of the night Pacific time.

PATH=/usr/local/sbin:/usr/local/bin:/sbin:/bin:/usr/sbin:/usr/bin

15 21 * * * root /usr/local/bin/neon-host-maintenance
EOF

chmod 644 /etc/cron.d/neon-host-maintenance

# Indicate that the script has completed.

endsetup setup-node
