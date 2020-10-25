#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-utility.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# Misc utility functions intended to be consumed by other scripts.

#------------------------------------------------------------------------------
# Invokes the command and arguments passed and checks the exit code.
# If the code is non-zero, the command and its output will be written
# to the standard error stream and then cause the calling script to
# exit with the code.

function safeinvoke()
{
    # $hack(jefflill):
    #
    # I need to disable the [set -e] flag within the method.  For
    # now, I'm just going to disable it at the beginning and then
    # enable it again before exiting.
    #
    # This assumes that the mode was enabled from the beginning.
    # It would be better to save and restore the original state.
    # This appears to be possible by examaining the [$-] variable.

    set +e

    # Invoke the command, logging the output and capturing the 
    # exit code.

    local logPath="$HOME/safeinvoke-`date --utc +%s-%N`.log"
    "$@" > $logPath 2>&1
    local exitCode=$?

    # Handle errors by sending the logged output to stderr
    # and be sure to delete the log file.

    if [ $exitCode -ne 0 ] ; then
        echo SAFEINVOKE FAILED: "$@" >&2
        echo EXIT CODE: $exitCode >&2
        echo "------------------------------" >&2
        cat $logPath >&2
        rm -f $logPath
        set -e
        exit $exitCode
    fi

    rm -f $logPath
    set -e
}

#------------------------------------------------------------------------------
# Invokes the command and arguments passed and checks the exit code.
# If the code is non-zero, the command and its output will be written
# to the standard error stream.
#
# The difference from [safeinvoke] is that this function will not 
# cause the calling script to exit for errors.  This is useful for
# situations where it's OK if a command fails.

function unsafeinvoke()
{
    # $hack(jefflill):
    #
    # I need to disable the [set -e] flag within the method.  For
    # now, I'm just going to disable it at the beginning and then
    # enable it again before exiting.
    #
    # This assumes that the mode was enabled from the beginning.
    # It would be better to save and restore the original state.
    # This appears to be possible by examaining the [$-] variable.

    set +e

    # Invoke the command, logging the output and capturing the 
    # exit code.

    local logPath="$HOME/unsafeinvoke-`date --utc +%s-%N`.log"
    "$@" > $logPath 2>&1
    local exitCode=$?

    # Handle errors by sending the logged output to stderr
    # and be sure to delete the log file.

    if [ $exitCode -ne 0 ] ; then
        echo UNSAFEINVOKE INFO: "$@" >&2
        echo EXIT CODE: $exitCode >&2
        echo "------------------------------" >&2
        cat $logPath >&2
        rm -f $logPath
        set -e
    fi

    rm -f $logPath
    set -e
}

#------------------------------------------------------------------------------
# Verifies that the setup step specified in the first parameter
# has not already been performed.  The current script will exit 
# with a zero exit code if the step has already completed successfully.
#
# Usage: startsetup OPERATION

function startsetup
{
    echo "*** START: ${1}" 1>&2

    mkdir -p ${NEON_STATE_FOLDER}/setup

    if [ -f "${NEON_STATE_FOLDER}/setup/$1" ] ; then
        echo "*** INFO: [$1] already completed." >&2
        exit 0
    fi
}

#------------------------------------------------------------------------------
# Indicates that the setup step specified in the first parameter
# has been completed.
#
# Usage: endsetup OPERATION

function endsetup
{
    echo "*** END: ${1}" 1>&2

    mkdir -p ${NEON_STATE_FOLDER}/setup
    touch "${NEON_STATE_FOLDER}/setup/$1"
}

#------------------------------------------------------------------------------
# Tests whether a file exists by returning [true] or [false] on standard output.
# This is useful for scripts running in strict mode where evaluating a bash "[...]"
# expression can result in the script exiting.
#
# Usage:    fileexists FILE

function fileexists
{
    setup-exists.sh -f $1
}

#------------------------------------------------------------------------------
# Tests whether a directory exists by returning [true] or [false] on standard output.
# This is useful for scripts running in strict mode where evaluating a bash "[...]"
# expression can result in the script exiting.
#
# Usage:    directoryexists FILE

function directoryexists
{
    setup-exists.sh -d $1
}

#------------------------------------------------------------------------------
# Ensures that a file contains a Bash script.  This is useful for verifying
# that script was downloaded correctly.  Sometimes these can be downloaded
# as an HTML error page if the URL is incorrect (e.g. the version specified
# does not exist).
#
# Usage:    bashscript FILE

function verifyscript
{
    exists=$(fileexists "$1")
    if [ "$exists" != "true" ] ; then
        echo "*** ERROR: [$1] does not exist." >&2
        exit 1
    fi

    # The first line should be: "#!/bin/bash" or "#!/bin/sh".

    regex="^(#!/bin/bash)|(#!/bin/sh)"

    while IFS='' read -r line || [[ -n "$line" ]]; 
    do
        echo $line
        if [[ $line =~ $regex ]] ; then
            return
        else
            echo "*** ERROR: [$1] is not a valid BASH script." >&2
            exit 1
        fi
    done < "$1"
}
