#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

# Handle the [version] command by printing the [package.json] file.

if [ "$1" == "version" ] ; then
    cat  /usr/share/kibana/package.json
    exit 0
fi

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ ! -f /etc/neon/env-host ] ; then
    . log-critical.sh "The [/etc/neon/env-host] file does not exist.  This file must have been generated on the Docker host by [neon-cli] during hive setup and be bound to the container."
    exit 1
fi

. /etc/neon/env-host

# Generate the configuration file.

. /usr/share/kibana/config/kibana.yml.sh

# Start Kibana

. log-info.sh "Starting [Kibana]"
/usr/share/kibana/bin/kibana
