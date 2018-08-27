#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching HAProxy.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the host node environment.

if [ -f /etc/neon/host-env ] ; then
    . /etc/neon/host-env
fi

# Load the neonHIVE constants.

. /neonhive.sh

# Initialize the configuration file check interval.

configInterval=${CONFIG_INTERVAL}

if [ "${configInterval}" == "" ] ; then
    configInterval=5
fi

if [ "${configInterval}" == "0" ] ; then
    . log-info.sh "HAProxy configuration change checking is disabled."
else
    . log-info.sh "HAProxy configuration change check interval: ${configInterval} seconds"
fi

configPath=/etc/haproxy/haproxy.cfg

set -e

# Compute the MD5 hash of the original configuration file so we can monitor
# it for changes.  This is useful when the configuration file is mounted
# from the container.

lastConfigHash=$(md5sum ${configPath} | awk '{print $1}')

# Validate the configuration file and then launch HAProxy for the first time.

. log-info.sh "Verifying configuration."

if ! haproxy -c -q -f ${configPath} ; then
    . log-critical.sh "Invalid initial HAProxy configuration."
    exit 1
fi

. log-info.sh "Starting HAProxy."

haproxyCommand="haproxy -f ${configPath} $@"
eval ${haproxyCommand}

# Monitor the configuration for changes and restart the proxy when we see any.
# Note that we'll report an error if the new configuration is invalid but we'll
# keep running with the previous settings for resiliency.

while true
do
    if [ "${configInterval}" == "0" ] ; then

        # Configuration change checking is disabled.

        sleep 300
        continue;
    fi

    newConfigHash=$(md5sum ${configPath} | awk '{print $1}')

    if [ "${lastConfigHash}" != "${newConfigHash}" ] ; then

        . log-info.sh "HAProxy configuration changed.  Restarting..."

        if haproxy -c -q -f ${configPath} ; then   # Validate the new configuration

            # Restart with the new config

            if ! eval ${haproxyCommand} -sf $(pidof haproxy) ; then
                
                . log-error.sh "Unable to restart HAProxy even though the configuration checked out.  Container will terminate."
                exit 1
            fi
        else
            . log-error.sh "Invalid HAProxy configuration change.  Retaining the old configuration."
        fi

        lastConfigHash=${newConfigHash}
    fi

    sleep ${configInterval}
done
