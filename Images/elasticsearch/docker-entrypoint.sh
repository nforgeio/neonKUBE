#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Loads the Docker host node environment variables before launching Elasticsearch
# so these values can be referenced by the Elasticsearch configuration file.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment.

if [ ! -f /etc/neon/host-env ] ; then
    . log-critical.sh "The [/etc/neon/host-env] file does not exist.  This file must be present on the hive host be mounted to the container."
    exit 1
fi

. /etc/neon/host-env

# Check the environment variables.

if [ "${ELASTICSEARCH_CLUSTER}" == "" ] ; then
    . log-critical.sh "ELASTICSEARCH_CLUSTER environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_TCP_PORT}" == "" ] ; then
    . log-critical.sh "ELASTICSEARCH_TCP_PORT environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_NODE_MASTER}" == "" ] ; then
    export ELASTICSEARCH_NODE_MASTER=true
fi

if [ "${ELASTICSEARCH_NODE_DATA}" == "" ] ; then
    export ELASTICSEARCH_NODE_DATA=true
fi

if [ "${ELASTICSEARCH_QUORUM}" == "" ] ; then
    . log-critical.sh "ELASTICSEARCH_QUORUM environment variable is missing."
    exit 1
fi

if [ "${ELASTICSEARCH_BOOTSTRAP_NODES}" == "" ] ; then
    . log-critical.sh "ELASTICSEARCH_BOOTSTRAP_NODES environment variable is missing."
    exit 1
fi

# We'll bind HTTP to all network interfaces.

ELASTICSEARCH_HTTP_HOST=0.0.0.0

# Path to the directory containing configuration files is specified via an
# environment variable for Elasticsearchy 6+

ES_PATH_CONF=/usr/share/elasticsearch/config

# The built-in Elasticsearch configuration environment variable subsititution
# mechanism has some issues, so we're going to use a script to explicitly 
# subsitute these variables and generate new config files.

. /usr/share/elasticsearch/config/elasticsearch.yml.sh

# We need to create this directory for some reason.

mkdir -p /usr/share/elasticsearch/config/scripts

# Ensure that the [/mnt/esdata] folder exists so the container will still
# function if no external Docker volume was mounted.

mkdir -p /mnt/esdata

# Log the settings.

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

# Start Elasticsearch under the [elasticsearch] user because it is
# not able to run as [root].
   
chown --recursive :elasticsearch /mnt/esdata/
chmod --recursive 770 /mnt/esdata/

chown --recursive :elasticsearch /usr/share/elasticsearch/
chmod --recursive 770 /usr/share/elasticsearch/

gosu elasticsearch /usr/share/elasticsearch/bin/elasticsearch
