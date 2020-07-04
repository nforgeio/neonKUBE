#!/bin/sh
#------------------------------------------------------------------------------
# Writes a WARN log message to standard output  if $LOG_LEVEL is set to WARN
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-warn.sh MESSAGE

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
        LOG=true
        ;;
    "INFO")
        LOG=true
        ;;
    "WARN")
        LOG=true
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
        LOG=false
        ;;
esac

. /log-index.sh

if [ "${LOG}" = "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [WARN] [index:${LOG_INDEX}] $1"
fi
