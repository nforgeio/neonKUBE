#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-prep-node.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script handles the configuration of a near-virgin Ubuntu 14.04 server 
# install into one suitable for deploying a neonHIVE cluster to.  This
# script requires that:
#
#       * OpenSSH was installed
#       * Hostname was left as the default: "ubuntu"
#       * [sudo] be configured to not request passwords

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/
#
# NOTE: I'm not using [-u] here to avoid failing for undefined
#       environment variables because some won't be initialized
#       when prepping nodes without a full hive definition.

set -eo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-PREP-NODE                          **" 1>&2
echo "**********************************************" 1>&2
echo

#------------------------------------------------------------------------------
# Disable the [apt-daily] service.  We're doing this for two reasons:
#
#   1. This service interferes with with [apt-get] usage during
#      hive setup and is also likely to interfere with end-user
#      configuration activities as well.
#
#   2. Automatic updates for production and even test hives is
#      just not a great idea.  You just don't want a random update
#      applied in the middle of the night that might cause trouble.

systemctl stop apt-daily.service
systemctl disable apt-daily.service
systemctl disable apt-daily.timer

#------------------------------------------------------------------------------
# We need to configure things such that [apt-get] won't complain
# about being unable to initialize Dialog when called from 
# non-interactive SSH sessions.

echo "** Configuring Dialog" 1>&2

echo 'debconf debconf/frontend select Noninteractive' | debconf-set-selections

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
# returning IPv6 addresses.  You'll need to prevent this on an
# application by application basis, like using the [curl -4] option.

sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf

#------------------------------------------------------------------------------
# Update the Bash profile so the global environment variables will be loaded
# into Bash sessions.

cat <<EOF > /etc/profile.d/env.sh
. /etc/environment
EOF

#------------------------------------------------------------------------------
# [sudo] doesn't allow the subprocess it creates to inherit the environment 
# variables by default.  You need to use the [-E] option to accomplish this.
# 
# As a convienence, we're going to create an [sbash] script, that uses
# [sudo] to start Bash while inheriting the current environment.

cat <<EOF > /usr/bin/sbash
# Starts Bash with elevated permissions while also inheriting
# the current environment variables.

/usr/bin/sudo -E bash \$@
EOF

chmod a+x /usr/bin/sbash

#------------------------------------------------------------------------------
# OpenVPN requires packet forwarding.

echo 1 > /proc/sys/net/ipv4/ip_forward

cat <<EOF >> /etc/sysctl.conf

###################################################################
# OpenVPN requires packet forwarding.

net.ipv4.ip_forward=1
EOF

#------------------------------------------------------------------------------
# Update the APT package index and install some common packages.

safe-apt-get update -yq
safe-apt-get install -yq unzip curl nano sysstat dstat iotop iptraf apache2-utils daemon

#------------------------------------------------------------------------------
# Clean some things up.

echo "** Clean up" 1>&2

# Clear any cached [apt-get] related files.

safe-apt-get clean -yq

# Clear any DHCP leases to be super sure that cloned node
# VMs will obtain fresh IP addresses.

rm -f /var/lib/dhcp/*.leases

echo "**********************************************" 1>&2
