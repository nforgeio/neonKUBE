#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-environment.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script manages the global environment variables stored in [/etc/environment].
# The commands are:
#
#       setup-environment.sh set NAME VALUE
#       setup-environment.sh remove NAME
#       setup-environment.sh remove-regex REGEX
#
# Note: A reboot is required for changes to take effect.
#
# The [set] command changes the value of an existing variable or
# adds a new one.
#
# The [remove] command removes a variable if it exists.
#
# The [remove-regex] removes variables whose names match a REGEX
# pattern.

environmentPath=/etc/environment

mkdir -p ${HOME}/temp
tempPath=${HOME}/temp/environment-`date --utc +%s-%N`.log

if [[ ${1} ]] ; then
    command=${1}
else
    command=
fi

# Implement the command.

case ${command} in

set)

    if [[ ${2} ]] ; then
        name=${2}
    else
        echo "ERROR[setup-environment]: NAME argument is required." >&2
        exit 1
    fi

    if [[ ${3} ]] ; then
        value=${3}
    else
        value=""
    fi

    regex="^${name}=.*$"
    found=false

    while IFS='' read -r line || [[ -n "${line}" ]]; 
    do
        if [[ ${line} =~ ${regex} ]] ; then
            echo "${name}=${value}" >> ${tempPath}
            found=true
        else
            echo ${line} >> ${tempPath}
        fi

    done < ${environmentPath}

    if ! ${found} ; then
        echo "${name}=${value}" >> ${tempPath}
    fi
    ;;

remove)

    if [[ ${2} ]] ; then
        name=${2}
    else
        echo "ERROR[setup-environment]: NAME argument is required." >&2
        exit 1
    fi

    regex="^${name}=.*$"

    while IFS='' read -r line || [[ -n "${line}" ]]; 
    do
        if ! [[ ${line} =~ ${regex} ]] ; then
            echo ${line} >> ${tempPath}
        fi

    done < ${environmentPath}
    ;;

remove-regex)

    if [[ ${2} ]] ; then
        regex=${2}
    else
        echo "ERROR[setup-environment]: REGEX argument is required." >&2
        exit 1
    fi

    regex="^${regex}=.*$"

    while IFS='' read -r line || [[ -n "${line}" ]]; 
    do
        if ! [[ ${line} =~ ${regex} ]] ; then
            echo ${line} >> ${tempPath}
        fi

    done < ${environmentPath}
    ;;

*)

    echo "ERROR[setup-environment]: Unknown command [${1}]." >&2
    exit 1
    ;;

esac

# Overwrite [/etc/environment] with the generated temporary file
# end then remove the temp file.

mv ${tempPath} ${environmentPath}
rm -f ${tempPath}

exit 0
