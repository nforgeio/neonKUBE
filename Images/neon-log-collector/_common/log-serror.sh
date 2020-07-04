#!/bin/sh
#------------------------------------------------------------------------------
# Writes an SERROR log message to standard output  if $LOG_LEVEL is set to SERROR
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-serror.sh MESSAGE

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
        LOG=true
        ;;
    "SERROR")
        LOG=true
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
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [SERROR] [index:${LOG_INDEX}] $1"
fi
