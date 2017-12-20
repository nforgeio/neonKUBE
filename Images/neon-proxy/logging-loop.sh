#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         logging-loop.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# USAGE:
#
#       logging-loop.sh SCRIPT MESSAGE
#
# ARGS:
#
#       SCRIPT      - the file name of the script to be used to log the message
#                     (e.g. "log-warning.sh")
#
#       MESSAGE     - the message to be logged
#
#
# This script is used to ensure that the container periodically logs warnings
# when it could not be reconfigured and is running with out-of-date settings.
# Pass the warning message as the script's first argument.
#
# The script saves it's PID to [/var/run/logging-loop.pid].

echo -n $$ > /var/run/logging-loop.pid

while :
do
    sleep ${WARN_SECONDS}
    . $1 "$2"
done
