#!/bin/sh
#------------------------------------------------------------------------------
# Writes a CRITICAL log message to standard output  if $LOG_LEVEL is set to CRITICAL
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-critical.sh MESSAGE

case "${LOG_LEVEL}" in
    "NONE")
        LOG=false
        ;;
    "DEBUG")
        LOG=true
        ;;
    "SINFO")
        LOG=true
        ;;
    "INFO")
        LOG=true
        ;;
    "WARN")
        LOG=true
        ;;
    "ERROR")
        LOG=true
        ;;
    "SERROR")
        LOG=true
        ;;
    "CRITICAL")
        LOG=true
        ;;
    *)
        LOG=true
        ;;
esac

. /log-index.sh

if [ "${LOG}" = "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [CRITICAL] [index:${LOG_INDEX}] $1"
fi
