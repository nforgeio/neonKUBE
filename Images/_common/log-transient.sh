#!/bin/sh
#------------------------------------------------------------------------------
# Writes a DEBUG log message to standard output if $LOG_LEVEL is set to TRANSIENT
# or higher.  INFO is assumed if $LOG_LEVEL is not set.
#
# USAGE: . log-transient.sh MESSAGE

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
        LOG=false
        ;;
esac

. /log-index.sh

if [ "${LOG}" = "true" ] ; then
    echo "[$(date --utc "+%Y-%m-%dT%H:%M:%S.000+00:00")] [TRANSIENT] [index:${LOG_INDEX}] $1"
fi
