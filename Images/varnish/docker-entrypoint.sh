#!/bin/bash
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

# Ensure that the [/var/lib/varnish/_.vsm_mgt] directory exists and has the correct
# permissions.  This is a fail-safe.  For production, this should have been
# mounted as a TMPFS with at least 90MB of space when the service was started.
#
# This craziness is required so that Varnish can compile and run its VCL files. 
# This needs to have execute permissions for all users.  We can't just mount a
# TMPFS to [/var/lib/varnish] because Docker doesn't currently (as of 10/23/2018)
# have a way to specify EXEC for a TMPFS mount for Swarm services:
#
#       https://github.com/moby/moby/pull/36720

if [ ! -d /var/lib/varnish/_.vsm_mgt ] ; then

    . log-warn.sh "For production, the [/var/lib/varnish/_.vsm_mgt] directory must be mounted to the service as a TMPFS with at least 90M of space and with permissions set to 755."

    mkdir -p /var/lib/varnish/_.vsm_mgt
    chmod 755 /var/lib/varnish/_.vsm_mgt
fi

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
