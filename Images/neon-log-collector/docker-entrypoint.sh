#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# Loads the Kubernetes host node environment variables before launching Fluent Bit
# so these values can be referenced by the TD-Agent configuration file.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ ! -f /etc/environment ] ; then
    . /log-critical.sh "The [/etc/environment] file does not exist.  This file must be present on the k8s host be mounted to the container."
    exit 1
fi

while read var; do export "${var}"; done < /etc/environment

# Log startup information.

. log-info.sh "Starting [neon-log-collector]"

if [ -f /geoip/database.mmdb.tar.gz ] ; then
    tar -xzf /geoip/database.mmdb.tar.gz -C /geoip/ --strip-components 1
fi

# Generate the index template file.

. /logstash-template.json.sh

# Launch td-agent.

# For systems without journald
mkdir -p /var/log/journal

# Use exec to get the signal
# A non-quoted string and add the comment to prevent shellcheck failures on this line.
# See https://github.com/koalaman/shellcheck/wiki/SC2086
# shellcheck disable=SC2086
exec /usr/sbin/td-agent
