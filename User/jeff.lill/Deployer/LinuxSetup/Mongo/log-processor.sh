#!/bin/bash
#
# log-processor.sh
#
# Handles the periodic processing of MONGODB and other related logs.
#
# Arguments:	None
#
# This script will be run periodically (currently every hour) to ensure
# that the MongoDB related log files don't grow too large.  This is a
# stop-gap measure.  Eventually, we'll want to upload these logs to 
# Elasticsearch or MDS/MDM.
#
# This script works for both ROUTER and DATA VM instances.
#
# This script requires root account privileges.

# $todo(jeff.lill): Implement a proper log rotation strategy

# Signal any running MONGOD and/or MONGOS instances to rotate to
# a new log file and then delete the old logs.

killall -SIGUSR1 mongos
killall -SIGUSR1 mongod

# Delete Mongo logs

rm /var/log/mongodb/*.log.*

# Delete TokuMX logs

rm /var/log/tokumx/*.log.*