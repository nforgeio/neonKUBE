#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the 
# [neon-proxy-manager] .NET service.

# Load the Docker host node environment.

if [ ! -f /etc/neon/host-env ] ; then
    . log-critical.sh "The [/etc/neon/host-env] file does not exist.  This file must have been generated on the Docker host by [neon-cli] during hive setup and be bound to the container."
    exit 1
fi

. /etc/neon/host-env

# Launch the service.

exec neon-varnish
