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
# This script actually ignores these and uses the backend name passed in the
# HAPROXY_SERVER_NAME as the hostname of the instance and HAPROXY_SERVER_PORT
# as the service port.  HAPROXY_SERVER_NAME is set to an host name like
# [manager-0.neon-vault.cluster] in the HAProxy config file.
#
# The script returns 0 if the endpoint is ready, non-zero if it's unavailable or sealed.
#
# NOTE: This relies on [/etc/neoncluster/env-host] being mounted and that it
#       adds the vault instance host definitions to the container's [/etc/hosts].

set -e
SEAL_STATUS=$(curl https://${HAPROXY_SERVER_NAME}:${HAPROXY_SERVER_PORT}/v1/sys/seal-status --silent --insecure --connect-timeout 5 --max-time 10)
SEAL_STATUS=$(echo ${SEAL_STATUS} | jq -r '.sealed')

if [ "${SEAL_STATUS}" = 'false' ]; then
    exit 0
else
    exit 1
fi
