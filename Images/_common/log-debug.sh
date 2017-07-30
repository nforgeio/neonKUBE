#!/bin/sh
#------------------------------------------------------------------------------
# Writes a DEBUG log message to standard output if $LOG_LEVEL is set to DEBUG
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-debug.sh MESSAGE

if [ "${LOG_LEVEL}" == "NONE" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "DEBUG" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "INFO" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "WARN" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "ERROR" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "CRITICAL" ] ; then
    LOG=false
else
    LOG=false
fi

. /log-index.sh

if [ "${LOG}" == "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [DEBUG] [index:${LOG_INDEX}] $1"
fi
