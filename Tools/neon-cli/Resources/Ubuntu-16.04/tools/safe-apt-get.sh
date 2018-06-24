#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         safe-apt-get
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Wraps the [apt-get] command such that the command is retried a few times 
# with a delay due to apparent network failures.  [apt-get] actually returns 
# [exitcode=0] when this happens but does report the problem as a warning 
# to STDOUT.  We'll look for this warning and act appropriately.
#
# USAGE: safe-apt-get ARGS
#
# where ARGS is the same as for [apt-get].

# Implementation Notes:
# ---------------------
# We're going to rety the operations a few times when a fetch error is reported,
# waiting a randomish number of seconds between a min/max.  We're going to use 
# the [shuf] command to generate this random number.
#
# We're also going to use a couple of fixed name temporary files to hold the
# output streams.  This means that multiple instances of this command won't
# be able to run in parallel (which probably won't work anyway).

RETRY_COUNT=5
RETRY_MIN_SECONDS=5
RETRY_MAX_SECONDS=30

STDOUT_PATH=/tmp/safe-apt-get-out
STDERR_PATH=/tmp/safe-apt-get-err
EXIT_CODE=0

# Delete any output files from any previous run.

if [ -f $STDOUT_PATH ] ; then
    rm $STDOUT_PATH
fi

if [ -f $STDERR_PATH ] ; then
    rm $STDERR_PATH
fi

# Peform the operation.

for i in {1..$RETRY_COUNT}
do
    if apt-get "$@" 1>$STDOUT_PATH 2>$STDERR_PATH
    then
        EXIT_CODE=$?

        if grep -q "^W: Failed to fetch" $STDERR_PATH
        then
            break       # Looks like there were no fetch problems.
        else
            if [ "$i" == "$RETRY_COUNT" ] ; then
                break;  # That was the last attempt.
            fi

            DELAY=$(shuf -i $RETRY_MIN_SECONDS-$RETRY_MAX_SECONDS -n 1)
            echo "*** safe-apt-get: retrying after fetch failure (delay=${DELAY}s)." >&2
            sleep $DELAY
        fi
    else
        # Looks like a hard failure.

        EXIT_CODE=$?
        break
    fi
done

# Return the captured output streams.

cat $STDOUT_PATH >&1
cat $STDERR_PATH >&2

# Delete the output files.

rm $STDOUT_PATH
rm $STDERR_PATH
        
exit $EXIT_CODE
