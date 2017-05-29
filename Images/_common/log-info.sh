#!/bin/sh
#------------------------------------------------------------------------------
# Writes an INFO log message to standard output  if $LOG_LEVEL is set to INFO
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-info MESSAGE

if [ "${LOG_LEVEL}" == "NONE" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "DEBUG" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "INFO" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "WARN" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "ERROR" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "FATAL" ] ; then
    LOG=false
else
    LOG=true
fi

if [ "${LOG}" == "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [INFO] $1"
fi
