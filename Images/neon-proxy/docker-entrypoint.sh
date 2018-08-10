#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching HAProxy.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Log startup information.

. log-info.sh "Starting [neon-proxy]"
. log-info.sh "CONFIG_KEY=${CONFIG_KEY}"
. log-info.sh "CONFIG_HASH_KEY=${CONFIG_HASH_KEY}"
. log-info.sh "VAULT_CREDENTIALS=${VAULT_CREDENTIALS}"
. log-info.sh "WARN_SECONDS=${WARN_SECONDS}"
. log-info.sh "START_SECONDS=${START_SECONDS}"
. log-info.sh "POLL_SECONDS=${POLL_SECONDS}"
. log-info.sh "LOG_LEVEL=${LOG_LEVEL}"
. log-info.sh "DEBUG=${DEBUG}"

# Run the hive host node environment script if present.

if [ -f /etc/neon/env-host ] ; then
    . /etc/neon/env-host
fi

# Load the neonHIVE constants.

. /neonhive.sh

# Verify that a CONFIG_KEY was passed.

if [ "${CONFIG_KEY}" == "" ] ; then
    . log-critical.sh "CONFIG_KEY environment variable is missing or empty."
    exit 1
fi

# Verify that a CONFIG_HASH_KEY was passed.

if [ "${CONFIG_HASH_KEY}" == "" ] ; then
    . log-critical.sh "CONFIG_HASH_KEY environment variable is missing or empty."
    exit 1
fi

# Verify that the Consul keys actually exist.

if ! consul kv get ${CONFIG_KEY} > /dev/nul ; then
    . log-critical.sh "[${CONFIG_KEY}] key cannot be retrieved from Consul."
    exit 1
fi

if ! consul kv get ${CONFIG_HASH_KEY} > /dev/nul ; then
    . log-critical.sh "[${CONFIG_HASH_KEY}] key cannot be retrieved from Consul."
    exit 1
fi

# Set the other environment variable defaults if necessary.

if [ "${WARN_SECONDS}" == "" ] ; then
    export WARN_SECONDS=300
fi

if [ "${POLL_SECONDS}" == "" ] ; then
    export POLL_SECONDS=15
fi

if [ "${START_SECONDS}" == "" ] ; then
    export START_SECONDS=10
fi

# Attempt Vault authentication using the VAULT_CREDENTIALS secret.  This
# will set VAULT_TOKEN if successful.
# 
# If VAULT_CREDENTIALS doesn't exist or is empty, then VAULT_TOKEN will
# be set to an empty string resulting in the container being unable to pull
# TLS certificates from Vault.  This mode is used for deploying the 
# [neon-proxy-public-bridge] and [neon-proxy-private-bridge] containers on 
# pet nodes to forward traffic from the pets to thw hive's Swarm.  This
# works because these proxies handle only TCP traffic.

if [ "${VAULT_CREDENTIALS}" != "" ] ; then
    . vault-auth.sh
else
    . log-info.sh "HTTPS routes are not supported because VAULT_CREDENTIALS is not specified or blank."
    export VAULT_TOKEN=
fi

# Ensure that the [/dev/shm/secrets] folder exists if one wasn't mounted 
# as a tmpfs.  [/dev/shm] stands for "shared memory" and is a built-in
# tmpfs with a capacity of 64MB, which should be plenty for most hives.

export SECRETS_TMP=/dev/shm/secrets
mkdir -p ${SECRETS_TMP}

# Create [/dev/shm/secrets/haproxy] to hold the HAProxy configuration and 
# [/dev/shm/secrets/haproxy-new] to temporarily hold the new configuration
# while it is being validated.

export CONFIG_FOLDER=${SECRETS_TMP}/haproxy
export CONFIG_NEW_FOLDER=${SECRETS_TMP}/haproxy-new

mkdir -p ${CONFIG_FOLDER}
mkdir -p ${CONFIG_NEW_FOLDER}

export CONFIG_PATH=${CONFIG_FOLDER}/haproxy.cfg
export CONFIG_NEW_PATH=${CONFIG_NEW_FOLDER}/haproxy.cfg

# Start a Consul watcher on the key, passing the [onconfigchange.sh] script.
# The command will call the script immediately and thereafter whenever the
# Consul key is modified (even if the same value is set again).
#
# [onconfigchange.sh] will download and validate the configuration within
# the [/dev/shm/secrets/haproxy-new] directory and then copy it to
# [/dev/shm/secrets/haproxy] before starting or restarting HAProxy.
#
# Note the the [consul watch] command returns only if [onconfigchange.sh]
# returns a non-zero exit code.

LAST_HASH=0d3001e6-3031-444f-8529-7f58a4faf179

while true
do
    NEW_HASH=$(consul kv get ${CONFIG_HASH_KEY})

    if [ "$?" != "0" ] ; then
        . log-warn.sh "The [${CONFIG_HASH_KEY}] key cannot be retrieved from Consul."
        sleep ${POLL_SECONDS}
        continue
    fi

    echo ${LAST_HASH} > ${SECRETS_TMP}/last-hash
    echo ${NEW_HASH}  > ${SECRETS_TMP}/new-hash

    if [ "${NEW_HASH}" != "${LAST_HASH}" ] ; then
        . log-info.sh "[${CONFIG_HASH_KEY}] hash changed to: [${NEW_HASH}]"
        . onconfigchange.sh
        LAST_HASH=${NEW_HASH}
    fi

    sleep ${POLL_SECONDS}
done
