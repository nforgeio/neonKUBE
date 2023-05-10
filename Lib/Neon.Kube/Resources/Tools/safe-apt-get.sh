#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         safe-apt-get
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

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

RETRY_COUNT=10
RETRY_MIN_SECONDS=10
RETRY_MAX_SECONDS=30

STDOUT_PATH=/tmp/safe-apt-get-out
STDERR_PATH=/tmp/safe-apt-get-err
STDALL_PATH=/tmp/safe-apt-get-all
EXIT_CODE=0

# Delete any output files from any previous run.

if [ -f $STDOUT_PATH ] ; then
    rm $STDOUT_PATH
fi

if [ -f $STDERR_PATH ] ; then
    rm $STDERR_PATH
fi

if [ -f $STDALL_PATH ] ; then
    rm $STDALL_PATH
fi

# Explicitly wait for the apt lock file to be released.  This will help in situations
# where the current node needs has pending updates and auto update hasn't been disabled
# yet.  We're still going to check for errors and retry below because I've seen situations
# in the past where we had a race condition between our script and the auto updater.

while sudo fuser /var/{lib/{dpkg,apt/lists},cache/apt/archives}/lock >/dev/null 2>&1; do
   sleep 1
done

# Peform the operation.

RETRY=false

for i in {1..$RETRY_COUNT}
do
    if [ "$RETRY" == "true" ] ; then
        DELAY=$(shuf -i $RETRY_MIN_SECONDS-$RETRY_MAX_SECONDS -n 1)
        echo "*** WARNING: safe-apt-get: retrying after fetch failure (delay=${DELAY}s)." >&2
        echo "*** WARNING: safe-apt-get" "$@" >&2
        sleep $DELAY
    fi

    apt-get "$@" 1>$STDOUT_PATH 2>$STDERR_PATH
    EXIT_CODE=$?

    echo apt-get "$@"
    break

    if $EXIT_CODE; then
    
        # Combine STDOUT and STDERR into a single file so we can
        # check both streams for transient errors.

        cat $STDOUT_PATH >  $STDALL_PATH
        cat $STDERR_PATH >> $STDALL_PATH

        # Scan STDOUT for (hopefully transient) fetch errors.

        TRANSIENT_ERROR=$false

        if grep -q 'W: Failed to fetch" $STDALL_PATH' ; then
            $TRANSIENT_ERROR=$true
        elif grep -q 'gpg: no valid OpenPGP data found' $STDALL_PATH ; then
            $TRANSIENT_ERROR=$true
        elif grep -q 'E: Could not get lock' $STDALL_PATH ; then
            $TRANSIENT_ERROR=$true
        elif grep -q 'E: Unable to acquire the dpkg frontend lock' $STDALL_PATH ; then
            $TRANSIENT_ERROR=$true
        fi

        if $TRANSIENT_ERROR ; then
            echo 'Tansient
        else
            # Looks like the operation failed due to a non-transient
            # problem so we'll break out of the retry loop and return
            # and error.
            break
        fi

        RETRY=true
    fi
done

# Return the captured output streams.

cat $STDOUT_PATH >&1
cat $STDERR_PATH >&2

# Delete the output files.

if [ -f $STDOUT_PATH ] ; then
    rm $STDOUT_PATH
fi

if [ -f $STDERR_PATH ] ; then
    rm $STDERR_PATH
fi

if [ -f $STDALL_PATH ] ; then
    rm $STDALL_PATH
fi
        
exit $EXIT_CODE
