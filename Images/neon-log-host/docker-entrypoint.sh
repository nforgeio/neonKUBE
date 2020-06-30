#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

# set log level if not set

if [ -z ${LOG_LEVEL+x} ]; then export LOG_LEVEL=info; fi

# Launch Fluent-bit.

. /log-info.sh "Starting: [neon-log-host]"
/fluent-bit/bin/fluent-bit -c /fluent-bit/etc/fluent-bit.conf
