#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Verify that cache TLS certificates have been mounted.

if [ ! -f /etc/neon-registry-cache/cache.crt ] ; then
    . log-error.sh "Expected [/etc/neon-registry-cache/cache.crt] to be mounted to the container."
    exit 1
fi

if [ ! -f /etc/neon-registry-cache/cache.key ] ; then
    . log-error.sh "Expected [/etc/neon-registry-cache/cache.key] to be mounted to the container."
    exit 1
fi

# Warn if no external data volume is mounted.

if [ ! -d /var/lib/neon-registry-cache ] ; then
    . log-warn.sh "Expected the registry data volume to mounted at [/var/lib/neon-registry-cache].  Production deployments should not persist the cache within the container."
    mkdir -p /var/lib/neon-registry-cache
fi

# Handle the environment variables. 

if [ "${HOSTNAME}" == "" ] ; then
    . log-error.sh "HOSTNAME environment variable is required."
    exit 1
fi

if [ "${REGISTRY}" == "" ] ; then
    export REGISTRY=https://registry-1.docker.io
fi

if [ "${LOG_LEVEL}" == "" ] ; then
    export LOG_LEVEL=info
fi

# Generate the registry configuration.

. registry.yml.sh

if [ "${USERNAME}" != "" ] ; then

    # Append the proxy config including the upstream credentials.

    cat <<EOF >> registry.yml
proxy:
    remoteurl: ${REGISTRY}
    username: ${USERNAME}
    password: ${PASSWORD}
EOF

else

    # Append the proxy config.

    cat <<EOF >> registry.yml
proxy:
    remoteurl: ${REGISTRY}
EOF

fi

# Start the registry.

. log-info.sh "Starting: [neon-registry-cache]"
registry serve registry.yml
