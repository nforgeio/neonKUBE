#!/bin/bash
#
# service-starter.sh
#
# Ensures that a specified service is running, restarting it necessary.
#
# Arguments:	$1		- name of the service
#				$2		- the name of the service process
#
# This is used for MONGOS deployments because Mongos will terminate if it 
# is not able to contact all of the shard configuration servers.  This
# script will be run frequently (every minute) as a CRON job to verify that
# the specified process is running and starting it if it isn't.
#
# This script requires root account privileges.

now=$(date --utc --iso-8601=seconds)

if ps -A | grep -i $2
    then
        echo [$1:$2] is running.
    else
        echo $(date): [$USER]: [$1:$2] is not running, restarting.
        /sbin/start $1
fi
