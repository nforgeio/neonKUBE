#!/bin/sh
#------------------------------------------------------------------------------
# Writes a FATAL log message to standard output  if $LOG_LEVEL is set to FATAL
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-fatal MESSAGE

if [ "${LOG_LEVEL}" == "NONE" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "DEBUG" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "INFO" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "WARN" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "ERROR" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "FATAL" ] ; then
    LOG=true
else
    LOG=true
fi

if [ "${LOG}" == "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [FATAL] $1"
fi
