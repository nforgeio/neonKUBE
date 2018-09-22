#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

if [ "${LOG_LEVEL}" == "" ] ; then
    export LOG_LEVEL=info
fi

# Log startup information.

. log-info.sh "Starting [neon-registry-cache]"

# Handle the environment variables. 

if [ "${HOSTNAME}" == "" ] ; then
    . log-error.sh "HOSTNAME environment variable is required."
    exit 1
fi

# Configure the upstream registry.

if [ "${REGISTRY}" == "" ] ; then
    export REGISTRY=https://registry-1.docker.io
fi

if [ "${REGISTRY}" == "docker.io" ] ; then
    . log-info.sh "Transforming HOSTNAME from [docker.io] to [https://registry-1.docker.io]"
    export REGISTRY=https://registry-1.docker.io
fi

# This is the [sh] compatible way of testing for:
#
#   "${REGISTRY}" =~ ^https://

if [ echo "${REGISTRY}" | grep -q '^https://' ] ; then
    . log-info.sh "Prefixing HOSTNAME with https://"
    export REGISTRY=https://${REGISTRY}
fi

. log-info.sh "HOSTNAME=${HOSTNAME}"
. log-info.sh "REGISTRY=${REGISTRY}"
. log-info.sh "USERNAME=${USERNAME}"
. log-info.sh "PASSWORD=**REDACTED**"
. log-info.sh "LOG_LEVEL=${LOG_LEVEL}"

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

# Generate the registry configuration.

. registry.yml.sh

if [ "${USERNAME}" != "" ] ; then

    # Append the proxy config including the upstream registry credentials.

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

. log-info.sh "Starting: [registry]"
registry serve registry.yml
