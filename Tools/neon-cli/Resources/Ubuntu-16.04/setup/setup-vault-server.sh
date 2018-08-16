#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-vault-server.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# Configures HashiCorp Vault secret server configured for high-availabilty
# mode on all manager nodes, using Consul as the backend.  This works in
# conjunction with the [neon-proxy-vault] HAProxy-based load balancing 
# service that will handle failover when a Vault fails on a manager.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-VAULT-SERVER                       **" 1>&2
echo "**********************************************" 1>&2

# Load the hive configuration and setup utilities.

. $<load-hive-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup vault

echo "*** BEGIN: Install Vault" 1>&2

#------------------------------------------------------------------------------
# Stop the service if it's running.

echo "***     Stopping Vault" 1>&2
unsafeinvoke systemctl stop vault

#------------------------------------------------------------------------------
# Download the Vault ZIP file to [/tmp] and then unzip and copy the binary
# to [/usr/local/bin] and make it executable.

echo "***     Downloading Vault" 1>&2

curl -4fsSLv ${CURL_RETRY} ${NEON_VAULT_DOWNLOAD} -o /tmp/vault.zip 1>&2
unzip -o /tmp/vault.zip -d /tmp
rm /tmp/vault.zip

mv /tmp/vault /usr/local/bin/vault
chmod 700 /usr/local/bin/vault

#------------------------------------------------------------------------------
# IMPORTANT:
#
# We need to prevent Vault memory from being swapped out to disk to prevent
# secrets from appearing unencrypted in the file system (this is also why
# we're not deploying Vault as a container).

echo "***     Prevent memory swap" 1>&2

setcap cap_ipc_lock=+ep $(readlink -f /usr/local/bin/vault)

#------------------------------------------------------------------------------
# Generate the config file.

echo "***     Generating config" 1>&2

mkdir -p /etc/vault

if [ "${NEON_CONSUL_TLS}" == "true" ] ; then
    CONSUL_SCHEME=https
else
    CONSUL_SCHEME=http
fi

cat <<EOF > /etc/vault/vault.hcl

cluster_name="${NEON_DATACENTER}.${NEON_HIVE}"
max_lease_ttl="${NEON_VAULT_MAXIMUM_LEASE}"
default_lease_ttl="${NEON_VAULT_DEFAULT_LEASE}"
ui=${NEON_VAULT_DASHBOARD}

listener "tcp" {

    address         = "0.0.0.0:${NEON_VAULT_PORT}"
    tls_cert_file   = "/etc/vault/vault.crt"
    tls_key_file    = "/etc/vault/vault.key"
}

backend "consul" {

    scheme          = "${CONSUL_SCHEME}"
    address         = "${NEON_NODE_NAME}:${NEON_CONSUL_PORT}"
    path            = "${NEON_VAULT_CONSUL_PATH}"
    tls_skip_verify = "true"
}
EOF

#------------------------------------------------------------------------------
# Generate the Vault server scripts.

echo "***     Generating server script" 1>&2

cat <<EOF > /usr/local/bin/vault-server
#!/bin/bash
#------------------------------------------------------------------------------
# Starts Vault in SERVER mode.

. $<load-hive-conf-quiet>

vault server                     \\
    -config=/etc/vault/vault.hcl \\
    -log-level=info
EOF

chmod 700 /usr/local/bin/vault-server

#------------------------------------------------------------------------------
# Generate the Vault systemd unit.

echo "***     Generating Vault systemd unit" 1>&2

cat <<EOF > /etc/systemd/system/vault.service
# HashiCorp Vault systemd unit file.

[Unit]
Description=HashiCorp Vault
Documentation=https://www.vaultproject.io/docs/index.html
After=network.target
Requires=

[Service]
ExecStart=/usr/local/bin/vault-server
ExecReload=/bin/kill -s HUP \$MAINPID
Restart=always

[Install]
WantedBy=multi-user.target
EOF

safeinvoke systemctl enable vault
systemctl daemon-reload

#------------------------------------------------------------------------------
# Start the service

echo "***     Starting Vault" 1>&2

safeinvoke systemctl start vault

#------------------------------------------------------------------------------
# Generate the [vault-direct] script which uses the VAULT_DIRECT_ADDR To
# communicate with the Vault cluster rather than VAULT_ADDR which depends
# on a running [neon-proxy-vault] service.
#
# For manager nodes, [vault-direct] will communicate with the Vault instance
# running on the current node.  [vault-direct] is intended for Vault cluster
# management purposes.  For most situations, relying on the standard [vault] 
# command taking advantage of [neon-proxy-vault] fail-over is best for production
# situations.

cat <<EOF > /usr/local/bin/vault-direct
#------------------------------------------------------------------------------
# Provides direct access to a Vault instance running on a manager node
# without relying on the [neon-proxy-vault] load balancer service.  This
# is intended for hive management purposes.
#
# This is a wrapper over the standard [vault] CLI and supports the
# same commands.

export VAULT_TLS_SERVER_NAME=${NEON_VAULT_HOSTNAME}
export VAULT_ADDR=${VAULT_DIRECT_ADDR}
vault "\$@"
EOF

chmod 700 /usr/local/bin/vault-direct

echo "*** END: Install Vault Server" 1>&2

# Indicate that the script has completed.

endsetup vault
