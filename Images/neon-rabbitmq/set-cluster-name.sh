#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         set-cluster-name.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script is executed by [docker-entrypoint.sh] to run in parallel while the
# RabbitMQ server spins up.  This script spins while waiting for RabbitMQ to
# report that it's ready and then it sets the cluster name.
#
# USAGE:    bash set-cluster-name.sh CLUSTERNAME &

set -e

while :
do
    if rabbitmqctl status > /dev/null 2>&1 ; then
        
        # The server is ready.
        
        rabbitmqctl set_cluster_name "$1" > /dev/null 2>&1
        exit 0
    fi

    sleep 1
done
