#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-exists.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
# This script tests for the existence of a file or directory and writes [true]
# or "false" to standard output.  This is intended to be used by the [fileexists]
# and [directoryexists] utility functions as a way to test existence while the
# parent script is running in strict mode.
#
# Usage:    setup-exists.sh -f FILE
#           setup-exists.sh -d DIRECTORY

case "${1}" in
-f)
    if [ -f "${2}" ] ; then
        echo true
    else
        echo false
    fi
    exit 0
    ;;

-d)
    if [ -d "${2}" ] ; then
        echo true
    else
        echo false
    fi
    ;;

*)
    echo "*** ERROR: ${0} $@" >&2
    exit 1
    ;;
esac
