#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the 
# [neon-hive-manager] .NET service.

# Load the Docker host node environment variables if present.

if [ -f /etc/neon/env-host ] ; then
    . /etc/neon/env-host
fi

# Load the [/etc/neon/env-container] environment variables if present.

if [ -f /etc/neon/env-container ] ; then
    . /etc/neon/env-container
fi

# Launch the service.

neon-hive-manager
