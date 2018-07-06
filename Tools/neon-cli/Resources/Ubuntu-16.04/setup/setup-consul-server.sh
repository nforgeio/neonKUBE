#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-consul-server.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script configures [Consul] on a manager node.
#
# Arguments:
#
#       encryption_key  - Consul encryption key (or "-" for none)

#------------------------------------------------------------------------------
# Configure Consul encryption.

if [ "${1}" == "-" ] ; then
    encryption_key=
else
    encryption_key=${1}
fi

if [ "${encryption_key}" != "-" ] ; then
    encrypt_option="-encrypt ${encryption_key}"
else
    encrypt_option=""
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

# Load the hive configuration and setup utilities.

. $<load-hive-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup consul-server

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

cat <<EOF > /etc/systemd/system/consul.service
[Unit]
Description=Consul Server service
Documentation=
After=
Requires=
Before=

[Service]
Type=simple
ExecStart=/usr/local/bin/consul agent -server -config-dir /etc/consul.d ${encrypt_option}
ExecReload=/bin/kill -s HUP \$MAINPID

[Install]
WantedBy=multi-user.target
EOF

#------------------------------------------------------------------------------
# Generate a small script that we'll use to start Consul.  We're doing this so
# we can initialize environment variables as necessary.

cat <<EOF > /usr/local/bin/consul.sh
#!/bin/bash

# Temporarily enable the new UI until HashiCorp enables it by default.
export CONSUL_UI_BETA=true

/usr/local/bin/consul "\$@"
EOF

chmod 700 /usr/local/bin/consul.sh

#------------------------------------------------------------------------------
# Configure and then start the Consul service for the first time.  This will
# add the encryption key (if there is one) to the key ring.

echo "*** Enable Consul" 1>&2
safeinvoke systemctl enable consul

echo "*** Start Consul" 1>&2
safeinvoke systemctl start consul

# We need to rewrite the systemd unit file to remove the encryption option
# to prevent problems with key ring conflicts after the Consul cluster has
# been formed and Consul is restarted.

cat <<EOF > /etc/systemd/system/consul.service
[Unit]
Description=Consul Proxy service
Documentation=
After=
Requires=
Before=

[Service]
Type=simple
ExecStart=/usr/local/bin/consul.sh agent -server -config-dir /etc/consul.d
ExecReload=/bin/kill -s HUP \$MAINPID

[Install]
WantedBy=multi-user.target
EOF

# Reload the service configuration to pick up the changes.

systemctl daemon-reload  

#------------------------------------------------------------------------------

# Indicate that the script has completed.

endsetup consul-server
