#------------------------------------------------------------------------------
# FILE:         elasticsearch.yml.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Generates the Elasticsearch configuration file [elasticsearch.yml] by
# substituting environment variables.  I ran into some trouble using 
# Elasticsearch's built-in subsitution mechanism and doing this has
# also makes it easier to manually verify the final configuration.

cat <<EOF > /usr/share/elasticsearch/config/elasticsearch.yml
##################### Elasticsearch Configuration #####################

# This file is intended to configure an Elasticsearch node within a 
# Docker container.  For this to work, the following environment
# variables must be specified when the container is first started:
#
#   ELASTICSEARCH_CLUSTER         - Identifies the cluster to be joined.
#   ELASTICSEARCH_NODE_NAME       - Name of the Elasticsearch node
#   ELASTICSEARCH_NODE_MASTER     - Indicates that this will be a master (true/false)
#   ELASTICSEARCH_NODE_DATA       - Indicates that this node will host data vs.
#                                   being a dedicated master or just a router (true/false)
#   ELASTICSEARCH_NODE_COUNT      - Number of nodes in the cluster
#   ELASTICSEARCH_SHARD_COUNT     - The shard count
#   ELASTICSEARCH_QUORUM          - The minimum number of master nodes to be
#                                   present for the cluster to be considered
#                                   healthy.
#   ELASTICSEARCH_BOOTSTRAP_NODES - A comma separated list of one or more IP addresses
#                                   or DNS names of nodes that will be used for 
#                                   bootstrapping the Elasticsearch cluster.
#
# Elasticsearch persists its data to the directory below.  This is
# where you should mount your external Docker volume:
#
#       /mnt/esdata

# Please see the documentation for further information on configuration options:
#
#   https://www.elastic.co/guide/en/elasticsearch/reference/6.0/settings.html
#   https://www.elastic.co/guide/en/elasticsearch/reference/6.0/modules.html

# ---------------------------------- Cluster -----------------------------------

# Cluster name identifies your cluster for auto-discovery. If you're running
# multiple clusters on the same network, be sure you're using unique names.

cluster.name: ${ELASTICSEARCH_CLUSTER}

# -------------------------------- X-Pack Settings ----------------------------

# xpack.security.enabled: false
# xpack.monitoring.enabled: false
# xpack.graph.enabled: false
# xpack.watcher.enabled: false
# xpack.reporting.enabled: false

# ------------------------------------ Node ------------------------------------

# Node names are generated dynamically on startup, so you're relieved
# from configuring them manually. You can tie this node to a specific name:

node.name: ${NEON_NODE_NAME}

# Allow this node to be eligible as a master node (enabled by default):

node.master: ${ELASTICSEARCH_NODE_MASTER}

# Allow this node to store data (enabled by default):

node.data: ${ELASTICSEARCH_NODE_DATA}

# A node can have generic attributes associated with it, which can later be used
# for customized shard allocation filtering, or allocation awareness. An attribute
# is a simple key value pair, similar to node.key: value, here is an example:

#node.rack: rack314

# By default, multiple nodes are allowed to start from the same installation location
# to disable it, set the following:

node.max_local_storage_nodes: 1

# ----------------------------------- Paths ------------------------------------

# Path to directory where node index data will be stored (a mounted Docker volume).

path.data: /mnt/esdata

# ----------------------------------- Memory -----------------------------------

# Elasticsearch performs poorly when JVM starts swapping.  If we were running on
# bare metal, we'd enable memory locking but I couldn't get this to work on
# Docker.  Instead, we're going to rely on the fact that neonHIVE nodes are
# configured with [swappiness=0] and that we'll reserve memory for our
# Elasticsearch service instance.

bootstrap.memory_lock: false

# ---------------------------------- Network -----------------------------------

# Network settings for internal cluster node-to-node (transport) communication.

transport.bind_host: 0.0.0.0
transport.publish_host: ${NEON_NODE_IP}
transport.tcp.port: ${ELASTICSEARCH_TCP_PORT}
transport.tcp.compress: true

# Network settings for the HTTP API interface.

http.host: 0.0.0.0
http.port: ${ELASTICSEARCH_HTTP_PORT}
http.enabled: true
http.max_content_length: 100mb

# --------------------------------- Discovery ----------------------------------

# Discovery infrastructure ensures nodes can be found within a hive
# and master node is elected.  Elasticsearch 5.0 supports only unicast 
# discovery.

# Set to ensure a node sees N other master eligible nodes to be considered
# operational within the cluster. This should be set to a quorum/majority of 
# the master-eligible nodes in the cluster.

discovery.zen.minimum_master_nodes: ${ELASTICSEARCH_QUORUM}

# Set the time to wait for ping responses from other nodes when discovering.
# Set this option to a higher value on a slow or congested network
# to minimize discovery failures:

discovery.zen.ping_timeout: 15s

# The ELASTICSEARCH_BOOTSTRAP_NODES environment variable is expected to have been
# set to the IP addresses or DNS hostnames of one or more of the Elasticsearch nodes 
# that will coordinate the mutual discovery of the cluster nodes.
#
#   ELASTICSEARCH_BOOTSTRAP_NODES=node0:port,node1:port",...

discovery.zen.ping.unicast.hosts: ${ELASTICSEARCH_BOOTSTRAP_NODES}

# ---------------------------------- Gateway -----------------------------------

# The gateway allows for persisting the cluster state between full cluster
# restarts. Every change to the state (such as adding an index) will be stored
# in the gateway, and when the cluster starts up for the first time,
# it will read its state from the gateway.

# Settings below control how and when to start the initial recovery process on
# a full cluster restart (to reuse as much local data as possible when using shared
# gateway).

# Allow recovery process after N nodes in a hive are up:

gateway.recover_after_nodes: ${ELASTICSEARCH_NODE_COUNT}

# Set the timeout to initiate the recovery process, once the N nodes
# from previous setting are up (accepts time value):

gateway.recover_after_time: 5m

# Set how many nodes are expected in this cluster. Once these N nodes
# are up (and recover_after_nodes is met), begin recovery process immediately
# (without waiting for recover_after_time to expire):

gateway.expected_nodes: ${ELASTICSEARCH_NODE_COUNT}

# ---------------------------------- Various -----------------------------------

# Disable Cross Origin Resource Sharing (CORS)

http.cors.enabled: false

# Require explicit names when deleting indices:

action.destructive_requires_name: true

EOF

chown elasticsearch:elasticsearch /usr/share/elasticsearch/config/*
chmod 644 /usr/share/elasticsearch/config/*
