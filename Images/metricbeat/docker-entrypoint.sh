#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment variables if present.

if [ -f /etc/neoncluster/env-host ] ; then
    . /etc/neoncluster/env-host
fi

# Load the [/etc/neoncluster/env-container] environment variables if present.

if [ -f /etc/neoncluster/env-container ] ; then
    . /etc/neoncluster/env-container
fi

# Load the neonCLUSTER constants.

. /neoncluster.sh

# Initialize the configuration defaults.

if [ "${ELASTICSEARCH_URL}" == "" ] ; then
    export ELASTICSEARCH_URL=http://${NeonHosts_LogEsData}:${NeonHostPorts_ProxyPrivateHttpLogEsData}
fi

if [ "${PERIOD}" == "" ] ; then
    export PERIOD=60s
fi

if [ "${PROCESSES}" == "" ] ; then
    export PROCESSES=[\'dockerd\',\'consul\',\'vault\']
fi

if [ "${LOG_LEVEL}" == "" ] ; then
    export LOG_LEVEL=info
else
    export LOG_LEVEL=$(echo ${LOG_LEVEL} | tr '[:upper:]' '[:lower:]')
fi

# Generate the Metricbeat config file.

/metricbeat.yml.sh

# Start [metricbeat] using the [--setup] option so that the the sample
# dashboards will be configured.

if [ "${1}" == "service" ] ; then

    . log-info.sh "Starting [Metricbeat]"
    . log-info.sh "ELASTICSEARCH_URL: ${ELASTICSEARCH_URL}"
    . log-info.sh "PERIOD: ${PERIOD}"
    . log-info.sh "PROCESSES: ${PROCESSES}"
    . log-info.sh "LOG_LEVEL: ${LOG_LEVEL}"

    /metricbeat -e -system.hostfs=/hostfs --setup
else 
    . log-error.sh "Invalid command line: $@"
    exit 1
fi
