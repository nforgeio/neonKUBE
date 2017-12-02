#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Executes the [/shim/__shim.sh] script that was mapped into the container
# but the [neon] tool running on the external workstation.
#
# IMPORTANT:
#
# The [/shim] folder is hardcoded in the [neon] tool's [DockerShim.ShimInternalFolder]
# property so don't change this folder in the container scripts without also updating
# the tool code.

# Execute the shimmed command.

if [ ! -f /shim/__shim.sh ] ; then
    echo "*** ERROR: The [neon-cli] image can only be invoked by a [neon-cli] shim running on the workstation."
    exit 1
fi

cd /shim
. ./__shim.sh "$@"
