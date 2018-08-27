#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching Kong.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ -f /etc/neon/host-env ] ; then
    . /etc/neon/host-env
fi

# Load the neonHIVE constants.

. /neonhive.sh

# Start the service.

. log-info.sh "Starting Kong API Gateway."
. kong-entrypoint.sh "$@"
