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

# Log startup information.

. log-info.sh "Starting [neon-log-collector]"

# Run the hive host node environment script.

if [ ! -f /etc/neon/env-host ] ; then
    . log-critical.sh "The [/etc/neon/env-host] file does not exist.  This file must have been generated on the Docker host by [neon-cli] during hive setup and be bound to the container."
    exit 1
fi

. /etc/neon/env-host

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
