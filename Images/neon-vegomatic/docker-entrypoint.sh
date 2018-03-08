#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching the 
# [neon-vegomatic] .NET service.

# Load the Docker host node environment variables if present.

if [ -f /etc/neoncluster/env-host ] ; then
    . /etc/neoncluster/env-host
fi

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Launch the service.

neon-vegomatic $@
