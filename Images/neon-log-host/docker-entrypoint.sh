#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching TD-Agent
# so these values can be referenced by the TD-Agent configuration file.

# Load the Docker host node environment variables.

if [ ! -f /etc/neoncluster/env-host ] ; then
    echo "[FATAL] The [/etc/neoncluster/env-host] file does not exist.  This file must have been generated on the Docker host by the [neon-cli] and be bound to the container." >&2
    exit 1
fi

. /etc/neoncluster/env-host

# Ensure that [/hostfs/var/log] was mounted and that the [neon-log-host]
# subdirectory exists for the journald position file.

if [ ! -d /hostfs/var/log ] ; then
    echo "[FATAL] The host [/var/log] directory has not been mounted to [/hostfs/var/log]." >&2
    exit 1
fi

mkdir -p /hostfs/var/log/neon-log-host

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the neonCLUSTER constants.

. /neoncluster.sh

# Launch TD-Agent.

td-agent
