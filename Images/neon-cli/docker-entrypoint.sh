#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

# Executes the [/shim/__shim.sh] script that was mapped into the container
# but the [neon] tool running on the external workstation.
#
# IMPORTANT:
#
# The [/shim] folder is hardcoded in the [neon] tool's [DockerShim.ShimInternalFolder]
# property so don't change this folder in the container scripts without also updating
# the tool code.

if [ ! -f /shim/__shim.sh ] ; then
    echo "*** ERROR: The [neon-cli] container cannot locate the script created by the [neon] tool."
    exit 1
fi

# Ensure that the [/cwd] directory exists in case this
# wasn't mounted into the container.

mkdir -p /cwd

# Invoke the command.

cd /shim
. ./__shim.sh "$@"
