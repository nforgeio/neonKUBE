#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-consul.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script configures [Consul] on a manager node.
#
# Arguments:
#
#       encryption_key  - Consul encryption key (or "-" for none)

# Get the encryption key.  Note that we're going to replace "-" 
# arguments values with empty strings because Bash doesn't 
# do empty arguments.

if [ "${1}" == "-" ] ; then
    encryption_key=
else
    encryption_key=${1}
fi

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-CONSUL-SERVER                      **" 1>&2
echo "**********************************************" 1>&2

# Load the cluster configuration and setup utilities.

. $<load-cluster-config>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup setup-consul-server

# Configure Consul.

#------------------------------------------------------------------------------
# Stop the service if it's running.

echo "***     Stop Consul" 1>&2
unsafeinvoke systemctl stop consul

#-----------------------------------------------------------------------------
# Download the Consul binary.

curl -4fsSLv ${CURL_RETRY} https://releases.hashicorp.com/consul/${NEON_CONSUL_VERSION}/consul_${NEON_CONSUL_VERSION}_linux_amd64.zip -o /tmp/consul.zip 1>&2
unzip -u /tmp/consul.zip -d /tmp
cp /tmp/consul /usr/local/bin
chmod 770 /usr/local/bin/consul

rm /tmp/consul.zip
rm /tmp/consul 

#------------------------------------------------------------------------------
# Make sure the data folder exists.

mkdir -p /mnt-data/consul
chmod 770 /mnt-data/consul

#------------------------------------------------------------------------------
# Generate the Consul systemd service unit (SERVER mode).

echo "*** Generating Consul Server systemd service unit" 1>&2

cat <<EOF > /lib/systemd/system/consul.service
[Unit]
Description=Consul Server service
Documentation=
After=
Requires=
Before=

[Service]
Type=simple
ExecStart=/usr/local/bin/consul agent -server -config-dir /etc/consul.d \\
    -encrypt ${encryption_key}
ExecReload=/bin/kill -s HUP \$MAINPID

[Install]
WantedBy=multi-user.target
EOF

cp /lib/systemd/system/consul.service /lib/systemd/system/consul.service.org

#------------------------------------------------------------------------------
# Configure and then start the Consul service for the first time.  This will
# add the encryption key (if there is one) to the key ring.

echo "*** Enable Consul" 1>&2
safeinvoke systemctl enable consul

echo "*** Start Consul" 1>&2
safeinvoke systemctl start consul

# We need to modify the systemd unit file by removing the encryption option
# to prevent problems with key ring conflicts after the Consul cluster has
# been formed and Consul is restarted.

sed -i '/\s*-encrypt\s.*/d' /lib/systemd/system/consul.service

# We can also remove the [--bootstrap-expect] after the first start so
# we won't see any warnings on restart.

sed -i '/\s*-bootstrap-expect\s.*/d' /lib/systemd/system/consul.service

# Reload the service configuration to pick up the changes.

systemctl daemon-reload  

#------------------------------------------------------------------------------

# Indicate that the script has completed.

endsetup setup-consul-server
