#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         hive.conf.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
# REQUIRES:     
#
# WARNING: This script is generated and managed by the [neon-cli] and
#          MUST NOT BE MODIFIED by hand unless you really know what you're doing.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script defines the current configuration of the neonHIVE as is
# currently known to this node.  [neon-cli] generates this during initial
# hive deployment and may modify it as the hive is reconfigured.
#
# This script also loads and exports environment variables from [/etc/environment]
# so they will be available to scripts invoked remotely by [neon-cli].
#
# Usage: hive.conf.sh [ --echo-summary ]

if [ "${1-none}" == "--echo-summary" ] ; then
    summary=true
else
    summary=false
fi

#------------------------------------------------------------------------------
# This identifies the tool/version that deployed or upgraded the hive.

NEON_HIVE_PROVISIONER=$<hive.provisioner>

#------------------------------------------------------------------------------
# Bash does not run interactively when called remotely via SSH.  This means
# that the global environment variables won't be loaded.  We need to do this
# ourselves.
#
# NOTE: We need to parse this rather than just running this file as a script 
#       because we need to export these variables for subprocesses.

while IFS='' read -r line || [[ -n "$line" ]]; 
do

    name=$(cut -d '=' -f 1 <<< "$line")
    value=$(cut -d '=' -f 2- <<< "$line")

    if [ "$name" != "" ] ; then
        declare -x "$name=$value"
    fi

done < /etc/environment

#------------------------------------------------------------------------------
# CURL download retry settings to make hive setup more robust in the
# face of transient network problems.

export CURL_RETRY="--retry 10 --retry-delay 30"

#------------------------------------------------------------------------------
# Describe important host machine folders.

export NEON_ARCHIVE_FOLDER=$<neon.folders.archive>
export NEON_BIN_FOLDER=$<neon.folders.bin>
export NEON_CONFIG_FOLDER=$<neon.folders.config>
export NEON_EXEC_FOLDER=$<neon.folders.exec>
export NEON_SETUP_FOLDER=$<neon.folders.setup>
export NEON_SECRETS_FOLDER=$<neon.folders.secrets>
export NEON_SCRIPTS_FOLDER=$<neon.folders.scripts>
export NEON_STATE_FOLDER=$<neon.folders.state>
export NEON_TMPFS_FOLDER=$<neon.folders.tmpfs>
export NEON_TOOLS_FOLDER=$<neon.folders.tools>

export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${NEON_SETUP_FOLDER}:${NEON_TOOLS_FOLDER}

#------------------------------------------------------------------------------
# Describe the hive manager nodes.  You can use the [getmanager] function
# below to retrieve node information using a zero-based index.
#
# You can access node properties using array syntax like:
#
#       ${managernode[name]}
#       ${managernode[address]}

export NEON_MANAGER_COUNT=$<nodes.manager.count>

$<nodes.managers>
#------------------------------------------------------------------------------
# Specify component specific settings

# NTP time sources to be configured on manager and worker nodes.

export NEON_NTP_MANAGER_SOURCES=( $<ntp.manager.sources> )
export NEON_NTP_WORKER_SOURCES=( $<ntp.worker.sources> )

# Consul settings

export NEON_CONSUL_VERSION=$<consul.version>
export NEON_CONSUL_OPTIONS=$<consul.options>
export NEON_CONSUL_ADDRESS=$<consul.address>
export NEON_CONSUL_FULLADDRESS=$<consul.fulladdress>
export NEON_CONSUL_HOSTNAME=$<consul.hostname>
export NEON_CONSUL_PORT=$<consul.port>
export NEON_CONSUL_TLS=$<consul.tls>

# Vault settings

export NEON_VAULT_VERSION=$<vault.version>
export NEON_VAULT_DOWNLOAD=$<vault.download>
export NEON_VAULT_HOSTNAME=$<vault.hostname>
export NEON_VAULT_PORT=$<vault.port>
export NEON_VAULT_CONSUL_PATH=$<vault.consulpath>
export NEON_VAULT_MAXIMUM_LEASE=$<vault.maximumlease>
export NEON_VAULT_DEFAULT_LEASE=$<vault.defaultlease>
export NEON_VAULT_DASHBOARD=$<vault.dashboard>

# Log settings

export NEON_LOG_ENABLED=$<log.enabled>

# Echo the configuration to STDERR if requested.

if $summary ; then
    echo "NEON_HIVE_PROVISIONER              = ${NEON_HIVE_PROVISIONER}" 1>&2
    echo 1>&2
    echo "NEON_HIVE                          = ${NEON_HIVE}" 1>&2
    echo "NEON_DATACENTER                    = ${NEON_DATACENTER}" 1>&2
    echo "NEON_ENVIRONMENT                   = ${NEON_ENVIRONMENT}" 1>&2
    echo "NEON_HOSTING                       = ${NEON_HOSTING}" 1>&2
    echo "NEON_NODE_NAME                     = ${NEON_NODE_NAME}" 1>&2
    echo "NEON_NODE_ROLE                     = ${NEON_NODE_ROLE}" 1>&2
    echo "NEON_NODE_FS                       = ${NEON_NODE_FS}" 1>&2
    echo "NEON_NODE_IP                       = ${NEON_NODE_IP}" 1>&2
    echo "NEON_NODE_SSD                      = ${NEON_NODE_SSD}" 1>&2
    echo "NEON_NODE_SWAP                     = ${NEON_NODE_SWAP}" 1>&2
    echo "NEON_UPSTREAM_DNS                  = ${NEON_UPSTREAM_DNS}" 1>&2
    echo "NEON_APT_PROXY                     = ${NEON_APT_PROXY}" 1>&2
    echo 1>&2
    echo "NEON_ARCHIVE_FOLDER                = ${NEON_ARCHIVE_FOLDER}" 1>&2
    echo "NEON_BIN_FOLDER                    = ${NEON_BIN_FOLDER}" 1>&2
    echo "NEON_CONFIG_FOLDER                 = ${NEON_CONFIG_FOLDER}" 1>&2
    echo "NEON_EXEC_FOLDER                   = ${NEON_EXEC_FOLDER}" 1>&2
    echo "NEON_SETUP_FOLDER                  = ${NEON_SETUP_FOLDER}" 1>&2
    echo "NEON_SECRETS_FOLDER                = ${NEON_SECRETS_FOLDER}" 1>&2
    echo "NEON_SCRIPTS_FOLDER                = ${NEON_SCRIPTS_FOLDER}" 1>&2
    echo "NEON_STATE_FOLDER                  = ${NEON_STATE_FOLDER}" 1>&2
    echo "NEON_TMPFS_FOLDER                  = ${NEON_TMPFS_FOLDER}" 1>&2
    echo "NEON_TOOLS_FOLDER                  = ${NEON_TOOLS_FOLDER}" 1>&2
    echo 1>&2
    echo "NEON_MANAGER_COUNT                 = ${NEON_MANAGER_COUNT}" 1>&2
$<nodes.manager.summary>
    echo "NEON_MANAGER_NAMES                 = ${NEON_MANAGER_NAMES[@]}" 1>&2
    echo "NEON_MANAGER_ADDRESSES             = ${NEON_MANAGER_ADDRESSES[@]}" 1>&2
    echo "NEON_MANAGER_PEERS                 = ${NEON_MANAGER_PEERS[@]}" 1>&2
    echo 1>&2
    echo "NEON_NTP_MANAGER_SOURCES           = ${NEON_NTP_MANAGER_SOURCES}" 1>&2
    echo "NEON_NTP_WORKER_SOURCES            = ${NEON_NTP_WORKER_SOURCES}" 1>&2
    echo 1>&2
    echo "NEON_CONSUL_VERSION                = ${NEON_CONSUL_VERSION}" 1>&2
    echo "NEON_CONSUL_OPTIONS                = ${NEON_CONSUL_OPTIONS}" 1>&2
    echo "NEON_CONSUL_ADDRESS                = ${NEON_CONSUL_ADDRESS}" 1>&2
    echo "NEON_CONSUL_FULLADDRESS            = ${NEON_CONSUL_FULLADDRESS}" 1>&2
    echo "NEON_CONSUL_HOSTNAME               = ${NEON_CONSUL_HOSTNAME}" 1>&2
    echo "NEON_CONSUL_PORT                   = ${NEON_CONSUL_PORT}" 1>&2
    echo "NEON_CONSUL_TLS                    = ${NEON_CONSUL_TLS}" 1>&2
    echo 1>&2 
    echo "NEON_VAULT_VERSION                 = ${NEON_VAULT_VERSION}" 1>&2
    echo "NEON_VAULT_DOWNLOAD                = ${NEON_VAULT_DOWNLOAD}" 1>&2
    echo "NEON_VAULT_DIRECT_ADDRESS          = ${VAULT_DIRECT_ADDR}" 1>&2
    echo "NEON_VAULT_HOSTNAME                = ${NEON_VAULT_HOSTNAME}" 1>&2
    echo "NEON_VAULT_PORT                    = ${NEON_VAULT_PORT}" 1>&2
    echo "NEON_VAULT_CONSUL_PATH             = ${NEON_VAULT_CONSUL_PATH}" 1>&2
    echo "NEON_VAULT_MAXIMUM_LEASE           = ${NEON_VAULT_MAXIMUM_LEASE}" 1>&2
    echo "NEON_VAULT_DEFAULT_LEASE           = ${NEON_VAULT_DEFAULT_LEASE}" 1>&2
    echo "NEON_VAULT_DASHBOARD               = ${NEON_VAULT_DASHBOARD}" 1>&2
    echo 1>&2
    echo "NEON_LOG_ENABLED                   = ${NEON_LOG_ENABLED}" 1>&2
    echo 1>&2
    echo "PATH                               = ${PATH}" 1>&2
fi

#------------------------------------------------------------------------------
# Define some useful global script functions.  Note that the special comments:
#
#       #<<<BEGIN-FUNCTIONS
#
#       #<<<END-FUNCTIONS
#
# are here to allow [neon-cli] to easily replace these with updated functions
# when necessary, in the future.

#------------------
#<<<BEGIN-FUNCTIONS

# Returns the manager information for a manager node based on its zero based
# index.  The result will be returned in the $MANAGE_NODE variable.
#
# Usage: getmanager INDEX

function getmanager
{
    eval MANAGE_NODE=$NEON_MANAGER_$1
}

#<<<END-FUNCTIONS
#------------------
