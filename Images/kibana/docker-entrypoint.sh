#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

# Enable exit on error.

set -e

# Generate the configuration file.

. /etc/kibana/kibana.yml.sh

# Add kibana as command if needed

if [[ "$1" == -* ]]; then
    set -- kibana "$@"
fi

# Run as user "kibana" if the command is "kibana"

if [ "$1" = 'kibana' ]; then  
    set -- gosu kibana tini -- "$@"
fi

exec "$@"
