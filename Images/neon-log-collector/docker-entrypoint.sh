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

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the neonCLUSTER constants.

. /neoncluster.sh

# Decompress the geoip database if one exists.

if [ -f /geoip/database.mmdb.gz ] ; then
    gunzip /geoip/database.mmdb.gz
fi

# Launch TD-Agent.

td-agent
