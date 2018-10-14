#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         report-error.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This script handles HAProxy configuration errors.
#
# USAGE:
#
#   report-error.sh [MESSAGE]
#
# This script looks examines ${RESTARTING} to determine if it's being reconfigured
# or started for the first time.  If it's being started, an ALERT will be logged to
# the standard output and the container will exit with a non-zero exit code.
#
# If HAProxy is being restarted, a WARNING will be logged immediately and the 
# [logging-loop.sh] script will be started to peridically issue fresh warnings
# while HAProxy's configuration remains out-of-date.  Note that this script
# writes its PID to [/var/run/logging-loop.pid] so that it can be killed when
# loading a new configuration (so we don't accumulate a bunch of running scripts).

if [ "$1" != "" ] ; then
    message=$1
else
    message="Unspecified Error"
fi

# Purge the contents of [/dev/shm/secrets/haproxy] and [/dev/shm/secrets/haproxy-new]
# before we exit so we don't leave secrets such as TLS key laying around in a 
# file system (even a tmpfs).

if [ "${DEBUG}" != "true" ] ; then
    rm -rf ${CONFIG_FOLDER}/*
    rm -rf ${CONFIG_NEW_FOLDER}/*
fi

# Log a warning message and start the logging loop if we're restarting, otherwise
# treat this is a fatal error. 

if [ "${RESTARTING}" == "true" ] ; then

    . log-warn.sh "${message}"
    logging-loop.sh "log-warn.sh" "HAProxy is running with an out-of-date configuration due to a previous error." &
    exit 0
else
    . log-critical.sh "${message}"

    if [ "${DEBUG}" != "true" ] ; then
        exit 1
    fi
fi
