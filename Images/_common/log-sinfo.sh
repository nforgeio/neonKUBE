#!/bin/sh
#------------------------------------------------------------------------------
# Writes an SINFO log message to standard output  if $LOG_LEVEL is set to SINFO
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-sinfo.sh MESSAGE

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
        LOG=false
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
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [SINFO] [index:${LOG_INDEX}] $1"
fi
