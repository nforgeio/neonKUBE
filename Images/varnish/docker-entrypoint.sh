#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Runs the [varnish] HTTP caching proxy with some simple configuration options.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the neonHIVE constants.

. /neonhive.sh

# Initialize environment variables.

if [ "${BACKEND_SERVER}" == "" ] ; then
    . log-error.sh "The [BACKEND_SERVER] environment variable is required."
    exit 1
fi

if [ "${BACKEND_PORT}" == "" ] ; then
    export BACKEND_PORT=80
fi

if [ "${MEMORY_LIMIT}" == "" ] ; then
    export MEMORY_LIMIT=100M
fi

. log-info.sh "Setting: BACKEND_SERVER=${BACKEND_SERVER}"
. log-info.sh "Setting: BACKEND_PORT=${BACKEND_PORT}"
. log-info.sh "Setting: MEMORY_LIMIT=${MEMORY_LIMIT}"

# Start Varnish

. log-info.sh "Starting: Varnish"
varnishd -a :80 -b ${BACKEND_SERVER}:${BACKEND_PORT} -s malloc,${MEMORY_LIMIT}
EXIT_CODE=$?

if [ "${EXIT_CODE}" != "0" ] ; then
    . log-error.sh "[varnishd] failed with [exit-code={${EXIT_CODE}]."
    exit ${EXIT_CODE}
fi

# Varnishd starts a background process that actually does the 
# proxying.  We'll spin quietly until that process exits.

while true
do
    if ! pidof varnishd > /dev/nul ; then
        break;
    fi

    sleep 5
done
