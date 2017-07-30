#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment variables.

if [ ! -f /etc/neoncluster/env-host ] ; then
    . log-critical.sh "The [/etc/neoncluster/env-host] file does not exist.  This file must have been generated on the Docker host by the [neon-cli] and be bound to the container."
    exit 1
fi

. /etc/neoncluster/env-host

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Generate the configuration file.

. /usr/share/kibana/config/kibana.yml.sh

# Start Kibana

. log-info.sh "Starting [Kibana]"
/usr/share/kibana/bin/kibana
