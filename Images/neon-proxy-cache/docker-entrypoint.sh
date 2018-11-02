#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the 
# [neon-proxy-cache] .NET service.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment.

if [ ! -f /etc/neon/host-env ] ; then
    . log-critical.sh "The [/etc/neon/host-env] file does not exist.  This file must be present on the hive host be mounted to the container."
    exit 1
fi

. /etc/neon/host-env

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

# Log the Varnish version.

varnishd -V

# Launch the service.

exec neon-proxy-cache
