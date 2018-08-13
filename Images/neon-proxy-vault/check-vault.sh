#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         check-vault.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Verifies that a specific Vault backend is healthy and ready for traffic.  This is
# called peridocially by HAProxy to add or remove instances from rotation.  See the
# detailed HAProxy documentation for more information:
#
#       http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#external-check%20command
#
# HAProxy will call this passing the following arguments:
#
#       <proxy_address> <proxy_port> <server_address> <server_port>
#
# This script actually ignores these and uses the backend name passed as the
# HAPROXY_SERVER_NAME environment variable as the hostname of the instance 
# and HAPROXY_SERVER_PORT as the service port.  HAPROXY_SERVER_NAME is set
# to an hostname like [manager-0.neon-vault.HIVENAME.hive] in the HAProxy 
# config file.
#
# The script returns 0 if the endpoint is ready, non-zero if it's unavailable, 
# sealed, or is not the active leader.
#
# NOTE: This relies on [/etc/neon/env-host] being mounted and that it adds the
#       vault instance host definitions to the container's [/etc/hosts].

set -e

# We're going to verify health via the [/v1/sys/health] API.  This returns  
# the OK/200 status when the instance is unsealed and active (the leader).

STATUS_CODE=$(curl -I -X HEAD https://${HAPROXY_SERVER_NAME}:${HAPROXY_SERVER_PORT}/v1/sys/health --silent --connect-timeout 1 --max-time 5 2> /dev/null | head -n 1|cut -d$' ' -f2)

if [ "${STATUS_CODE}" == '200' ]; then
    exit 0
else
    exit 1
fi

