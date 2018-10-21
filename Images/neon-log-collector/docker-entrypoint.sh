#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching TD-Agent
# so these values can be referenced by the TD-Agent configuration file.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ ! -f /etc/neon/host-env ] ; then
    . log-critical.sh "The [/etc/neon/host-env] file does not exist.  This file must be present on the hive host be mounted to the container."
    exit 1
fi

. /etc/neon/host-env

# Log startup information.

. log-info.sh "Starting [neon-log-collector]"

# Load the neonHIVE constants.

. /neonhive.sh

# Decompress the geoip database if one exists.

if [ -f /geoip/database.mmdb.gz ] ; then
    gunzip /geoip/database.mmdb.gz
fi

# Generate the index template file.

. /logstash-template.json.sh

# Launch TD-Agent.

td-agent
