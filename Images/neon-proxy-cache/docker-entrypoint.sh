#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Runs varnish-cache

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

# Start varnish

. log-info.sh "Starting: Varnish"
varnish -b ${BACKEND_SERVER}:${BACKEND_PORT} -s malloc,${MEMORY_LIMIT}

if [ "$?" != "=" ] ; then
    EXIT_CODE = $?
    . log-error.sh "[varnishd] failed with [exit-code={$?}]."
    exit ${EXIT_CODE}
fi
