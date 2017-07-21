#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching Elasticsearch
# so these values can be referenced by the Elasticsearch configuration file.

# Load the Docker host node environment variables.

if [ ! -f /etc/neoncluster/env-host ] ; then
    echo "[FATAL] The [/etc/neoncluster/env-host] file does not exist.  This file must have been generated on the Docker host by the [neon-cli] and be bound to the container." >&2
    exit 1
fi

. /etc/neoncluster/env-host

LOG_LEVEL=INFO

# Check the environment variables.

if [ "${ELASTICSEARCH_CLUSTER}" == "" ] ; then
    . log-fatal.sh "ELASTICSEARCH_CLUSTER environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_TCP_PORT}" == "" ] ; then
    . log-fatal.sh "ELASTICSEARCH_TCP_PORT environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_NODE_MASTER}" == "" ] ; then
    export ELASTICSEARCH_NODE_MASTER=true
fi

if [ "${ELASTICSEARCH_NODE_DATA}" == "" ] ; then
    export ELASTICSEARCH_NODE_DATA=true
fi

if [ "${ELASTICSEARCH_QUORUM}" == "" ] ; then
    . log-fatal.sh "ELASTICSEARCH_QUORUM environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_BOOTSTRAP_NODES}" == "" ] ; then
    . log-fatal.sh "ELASTICSEARCH_BOOTSTRAP_NODES environment variable is missing."
    exit 1
fi

# Add the root directory to the PATH.

PATH=${PATH}:/

# Enable exit on error.

set -e

# We'll bind HTTP to all network interfaces.

ELASTICSEARCH_HTTP_HOST=0.0.0.0

#------------------------------------------------------------------------------
# The built-in Elasticsearch configuration environment variable subsititution
# mechanism has some issues, so we're going to use a scripts to explicitly 
# subsitute these veriables and generate new config files.

. /usr/share/elasticsearch/config/elasticsearch.yml.sh

#------------------------------------------------------------------------------
# This a tweaked version the original script from the base Elasticsearch image:

# Add elasticsearch command if needed
if [ "${1:0:1}" = '-' ]; then
    set -- elasticsearch "$@"
fi

# Ensure that the [/mnt/esdata] folder exists so the container will still
# function if no external Docker volume was mounted.
mkdir -p /mnt/esdata

# Drop root privileges if we are running elasticsearch
# allow the container to be started with `--user`
if [ "$1" = 'elasticsearch' -a "$(id -u)" = '0' ]; then

    # Change the ownership of /mnt/esdata to elasticsearch
    chown -R elasticsearch:elasticsearch /mnt/esdata

    . log-info.sh "Starting [Elasticsearch]"
    . log-info.sh "ELASTICSEARCH_CLUSTER: ${ELASTICSEARCH_CLUSTER}"
    . log-info.sh "ELASTICSEARCH_NODE_MASTER: ${ELASTICSEARCH_NODE_MASTER}"
    . log-info.sh "ELASTICSEARCH_NODE_DATA: ${ELASTICSEARCH_NODE_DATA}"
    . log-info.sh "ELASTICSEARCH_TCP_PORT: ${ELASTICSEARCH_TCP_PORT}"
    . log-info.sh "ELASTICSEARCH_HTTP_PORT: ${ELASTICSEARCH_HTTP_PORT}"
    . log-info.sh "ELASTICSEARCH_NODE_COUNT: ${ELASTICSEARCH_NODE_COUNT}"
    . log-info.sh "ELASTICSEARCH_QUORUM: ${ELASTICSEARCH_QUORUM}"
    . log-info.sh "ELASTICSEARCH_BOOTSTRAP_NODES: ${ELASTICSEARCH_BOOTSTRAP_NODES}"
    . log-info.sh "ES_JAVA_OPTS: ${ES_JAVA_OPTS}"
    
    set -- gosu elasticsearch "$@"
fi

# As argument is not related to elasticsearch,
# then assume that user wants to run his own process,
# for example a `bash` shell to explore this image
exec "$@"
