#!/bin/sh
#------------------------------------------------------------------------------
# Writes a WARN log message to standard output  if $LOG_LEVEL is set to WARN
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-warn MESSAGE

if [ "${LOG_LEVEL}" == "NONE" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "DEBUG" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "INFO" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "WARN" ] ; then
    LOG=true
elif [ "${LOG_LEVEL}" == "ERROR" ] ; then
    LOG=false
elif [ "${LOG_LEVEL}" == "FATAL" ] ; then
    LOG=false
else
    LOG=true
fi

if [ "${LOG}" == "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [WARN] $1"
fi
