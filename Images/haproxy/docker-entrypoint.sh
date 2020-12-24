#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Loads the Docker host node environment variables before launching HAProxy.

# Add the root directory to the PATH.

PATH=${PATH}:/

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

haproxy -f ${configPath} &

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

            haproxy -f ${configPath} -sf $(pidof haproxy)
        else
            . log-error.sh "Invalid HAProxy configuration change.  Retaining the old configuration."
        fi

        lastConfigHash=${newConfigHash}
    fi

    sleep ${configInterval}
done
