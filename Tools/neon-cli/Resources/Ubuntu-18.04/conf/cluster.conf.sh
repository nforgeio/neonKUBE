#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         cluster.conf.sh
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

# REQUIRES:     
#
# WARNING: This script is generated and managed by the [neon-cli] and
#          MUST NOT BE MODIFIED by hand unless you really know what you're doing.
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script defines the current configuration of the cluster as is
# currently known to this node.  [neon-cli] generates this during initial
# cluster deployment and may modify it as the cluster is reconfigured.
#
# This script also loads and exports environment variables from [/etc/environment]
# so they will be available to scripts invoked remotely by [neon-cli].
#
# Usage: cluster.conf.sh [ --echo-summary ]

if [ "${1-none}" == "--echo-summary" ] ; then
    summary=true
else
    summary=false
fi

#------------------------------------------------------------------------------
# This identifies the tool/version that deployed or upgraded the cluster.

NEON_HIVE_PROVISIONER=$<cluster.provisioner>

#------------------------------------------------------------------------------
# Bash does not run interactively when called remotely via SSH.  This means
# that the global environment variables won't be loaded.  We need to do this
# ourselves.
#
# NOTE: We need to parse this rather than just running this file as a script 
#       because we need to export these variables for subprocesses.

while IFS='' read -r line || [[ -n "$line" ]]; 
do

    name=$(cut -d '=' -f 1 <<< "$line")
    value=$(cut -d '=' -f 2- <<< "$line")

    if [ "$name" != "" ] ; then
        declare -x "$name=$value"
    fi

done < /etc/environment

#------------------------------------------------------------------------------
# CURL download retry settings to make cluster setup more robust in the
# face of transient network problems.

export CURL_RETRY="--retry 10 --retry-delay 30"

#------------------------------------------------------------------------------
# Describe important host machine folders.

export NEON_ARCHIVE_FOLDER=$<neon.folders.archive>
export NEON_BIN_FOLDER=$<neon.folders.bin>
export NEON_CONFIG_FOLDER=$<neon.folders.config>
export NEON_EXEC_FOLDER=$<neon.folders.exec>
export NEON_SETUP_FOLDER=$<neon.folders.setup>
export NEON_STATE_FOLDER=$<neon.folders.state>
export NEON_TMPFS_FOLDER=$<neon.folders.tmpfs>

export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:${NEON_SETUP_FOLDER}:${NEON_BIN_FOLDER}

#------------------------------------------------------------------------------
# Describe the cluster master nodes.  You can use the [getmaster] function
# below to retrieve node information using a zero-based index.
#
# You can access node properties using array syntax like:
#
#       ${masternode[name]}
#       ${masternode[address]}

export NEON_MASTER_COUNT=$<nodes.master.count>

$<nodes.masters>
#------------------------------------------------------------------------------
# Specify component specific settings

# NTP time sources to be configured on master and worker nodes.

export NEON_NTP_MASTER_SOURCES=( $<ntp.master.sources> )
export NEON_NTP_WORKER_SOURCES=( $<ntp.worker.sources> )

# Log settings

export NEON_LOG_ENABLED=$<log.enabled>

# Echo the configuration to STDERR if requested.

if $summary ; then
    echo "NEON_CLUSTER_PROVISIONER           = ${NEON_CLUSTER_PROVISIONER}" 1>&2
    echo 1>&2
    echo "NEON_CLUSTER                       = ${NEON_CLUSTER}" 1>&2
    echo "NEON_DATACENTER                    = ${NEON_DATACENTER}" 1>&2
    echo "NEON_ENVIRONMENT                   = ${NEON_ENVIRONMENT}" 1>&2
    echo "NEON_HOSTING                       = ${NEON_HOSTING}" 1>&2
    echo "NEON_NODE_NAME                     = ${NEON_NODE_NAME}" 1>&2
    echo "NEON_NODE_ROLE                     = ${NEON_NODE_ROLE}" 1>&2
    echo "NEON_NODE_IP                       = ${NEON_NODE_IP}" 1>&2
    echo "NEON_NODE_HDD                      = ${NEON_NODE_HDD}" 1>&2
    echo "NEON_PACKAGE_PROXY                 = ${NEON_PACKAGE_PROXY}" 1>&2
    echo 1>&2
    echo "NEON_ARCHIVE_FOLDER                = ${NEON_ARCHIVE_FOLDER}" 1>&2
    echo "NEON_BIN_FOLDER                    = ${NEON_BIN_FOLDER}" 1>&2
    echo "NEON_CONFIG_FOLDER                 = ${NEON_CONFIG_FOLDER}" 1>&2
    echo "NEON_EXEC_FOLDER                   = ${NEON_EXEC_FOLDER}" 1>&2
    echo "NEON_SETUP_FOLDER                  = ${NEON_SETUP_FOLDER}" 1>&2
    echo "NEON_STATE_FOLDER                  = ${NEON_STATE_FOLDER}" 1>&2
    echo "NEON_TMPFS_FOLDER                  = ${NEON_TMPFS_FOLDER}" 1>&2
    echo 1>&2
    echo "NEON_MASTER_COUNT                  = ${NEON_MASTER_COUNT}" 1>&2
    echo "NEON_MASTER_ADDRESSES              = ${NEON_MASTER_ADDRESSES[@]}" 1>&2
$<nodes.master.summary>
    echo "NEON_MASTER_NAMES                  = ${NEON_MASTER_NAMES[@]}" 1>&2
    echo 1>&2
    echo "NEON_NTP_MASTER_SOURCES            = ${NEON_NTP_MASTER_SOURCES}" 1>&2
    echo "NEON_NTP_WORKER_SOURCES            = ${NEON_NTP_WORKER_SOURCES}" 1>&2
    echo 1>&2
    echo "PATH                               = ${PATH}" 1>&2
fi

#------------------------------------------------------------------------------
# Define some useful global script functions.  Note that the special comments:
#
#       #<<<BEGIN-FUNCTIONS
#
#       #<<<END-FUNCTIONS
#
# are here to allow [neon-cli] to easily replace these with updated functions
# if necessary in the future.

#------------------
#<<<BEGIN-FUNCTIONS

# Returns the information for a master node based on its zero based
# index.  The result will be returned in the $MANAGE_NODE variable.
#
# Usage: getmaster INDEX

function getmaster
{
    eval MASTER_NODE=$NEON_MASTER_$1
}

#<<<END-FUNCTIONS
#------------------
