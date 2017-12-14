#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching HAProxy.

. log-info.sh "Starting [neon-proxy]"
. log-info.sh "CONFIG_KEY=${CONFIG_KEY}"
. log-info.sh "VAULT_CREDENTIALS=${VAULT_CREDENTIALS}"
. log-info.sh "WARN_SECONDS=${WARN_SECONDS}"
. log-info.sh "START_SECONDS=${START_SECONDS}"
. log-info.sh "LOG_LEVEL=${LOG_LEVEL}"
. log-info.sh "DEBUG=${DEBUG}"

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment variables if present.

if [ -f /etc/neoncluster/env-host ] ; then
    . /etc/neoncluster/env-host
fi

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Load the neonCLUSTER constants.

. /neoncluster.sh

# Verify that a CONFIG_KEY was passed.

if [ "${CONFIG_KEY}" == "" ] ; then
    . log-critical.sh "CONFIG_KEY environment variable is missing or empty."
    exit 1
fi

# Verify that the key actually exists.

if ! consul kv get ${CONFIG_KEY} > /dev/nul ; then
    . log-critical.sh "The [${CONFIG_KEY}] key cannot be retrieved from Consul."
    exit 1
fi

# Load the other environment variable parameters.

if [ "${WARN_SECONDS}" == "" ] ; then
    export WARN_SECONDS=300
fi

if [ "${START_SECONDS}" == "" ] ; then
    export START_SECONDS=10
fi

# Attempt Vault authentication using the ${VAULT_CREDENTIALS} secret.  This
# will set ${VAULT_TOKEN} if successful.
# 
# If ${VAULT_CREDENTIALS} doesn't exist or is empty, then ${VAULT_TOKEN} will
# be set to an empty string resulting in the container being unable to pull
# TLS certificates from Vault.  This mode is used for deploying the 
# [neon-proxy-public-bridge] and [neon-proxy-private-bridge] containers on 
# pet nodes to forward traffic from the pets to thw cluster's Swarm.  This
# works because these proxies handle only TCP traffic.

if [ "${VAULT_CREDENTIALS}" != "" ] ; then
    . vault-auth.sh
else
    . log-info.sh "HTTPS routes are not supported because VAULT_CREDENTIALS is not specified or blank."
    export VAULT_TOKEN=
fi

# Ensure that the [/tmp/secrets] folder exists if one wasn't mounted as a tmpfs.

export SECRETS_TMP=/tmp/secrets
mkdir -p ${SECRETS_TMP}

# Create [/tmp/secrets/haproxy] to hold the HAProxy configuration and 
# [/tmp/secrets/haproxy-new] to temporarily hold the new configuration
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
# the [/tmp/secrets/haproxy-new] directory and then copy it to [/tmp/secrets/haproxy]
# before starting or restarting HAProxy.
#
# Note the the [consul watch] command returns only if [onconfigchange.sh]
# returns a non-zero exit code.

consul watch -type=key -key=${CONFIG_KEY} onconfigchange.sh
