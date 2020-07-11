#!/bin/sh
#------------------------------------------------------------------------------
# Writes an INFO log message to standard output  if $LOG_LEVEL is set to INFO
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-info.sh MESSAGE

case "${LOG_LEVEL}" in
    "NONE")
        LOG=false
        ;;
    "DEBUG")
        LOG=true
        ;;
    "TRANSIENT")
        LOG=true
        ;;
    "SINFO")
        LOG=false
        ;;
    "INFO")
        LOG=true
        ;;
    "WARN")
        LOG=false
        ;;
    "ERROR")
        LOG=false
        ;;
    "SERROR")
        LOG=false
        ;;
    "CRITICAL")
        LOG=false
        ;;
    *)
        LOG=true
        ;;
esac

. /log-index.sh

if [ "${LOG}" = "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [INFO] [index:${LOG_INDEX}] $1"
fi
