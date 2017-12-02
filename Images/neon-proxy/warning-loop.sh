#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         warning-loop.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# This script is used to ensure that the container periodically logs warnings
# when it could not be reconfigured and is running with out-of-date settings.
# Pass the warning message as the script's first argument.
#
# The script saves it's PID to [/var/run/warning-loop.pid].

echo -n $$ > /var/run/warning-loop.pid

while :
do
    sleep ${WARN_SECONDS}
    . log-warn.sh "$1"
done
