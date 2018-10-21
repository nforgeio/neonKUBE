#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment.

if [ ! -f /etc/neon/host-env ] ; then
    . log-critical.sh "The [/etc/neon/host-env] file does not exist.  This file must be present on the hive host be mounted to the container."
    exit 1
fi

. /etc/neon/host-env

# Launch the service.

exec neon-dns-mon
