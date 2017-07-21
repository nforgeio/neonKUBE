#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This is a template of the docker image definition file to be used to generate
# a modified Elasticsearch image based on a specific official version of the
# ElasticSearch image.  This image includes:
#
#   * A custom entry point script that loads environment variables and other
#     settings via the mounted [/etc/neoncluster/env-host] and
#     [/etc/neoncluster/env-host] files.
#
#   * Elasticsearch X-PACK plugins.
#
# ARGUMENTS:
#
#   VERSION     - The Elasticsearch version (e.g. "5.0.1")
#
# REMARKS:
#
# You need to specify the following environment variables when running the 
# container (these are referenced by the [elasticsearch.yml] configuration file):
#
#   ELASTICSEARCH_CLUSTER         - (required) Identifies the Elasticsearch cluster
#   ELASTICSEARCH_TCP_PORT        - (required) Inter-node TCP communication ports
#   ELASTICSEARCH_HTTP_PORT       - (required) HTTP API port
#   ELASTICSEARCH_NODE_MASTER     - (optional) Indicates that this is eligible to be a master
#                                   Defaults to [true].
#   ELASTICSEARCH_NODE_DATA       - (optional) Indicates that this node will host data vs.
#                                   being a dedicated master or just a router
#                                   Defaults to [true].
#   ELASTICSEARCH_QUORUM          - (required) Minimum number of master nodes to be
#                                   present for the cluster to be considered
#                                   healthy.
#   ELASTICSEARCH_BOOTSTRAP_NODES - (required) A comma separated list of one or more IP 
#                                   addresses or DNS names of nodes that will be used 
#                                   for bootstrapping the Elasticsearch cluster.
#
# The Docker data volume must be mounted at:
#
#       /mnt/esdata

ARG         VERSION
FROM        elasticsearch:${VERSION}
MAINTAINER  jeff@lilltek.com
ARG         VERSION

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && apt-get update \
    && apt-get install -yq wget \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

# Overwrite the default Elasticsearch entrypoint to load the Docker host environment 
# variables from [/etc/neoncluster/node-env] so they can be referenced by the Elasticsearch
# configuration file.

# Install X-PACK.

RUN wget -nv https://artifacts.elastic.co/downloads/packs/x-pack/x-pack-${VERSION}.zip -O /tmp/x-pack.zip \
    && cd /usr/share/elasticsearch \
    && bin/elasticsearch-plugin install --batch file:///tmp/x-pack.zip \
    && rm /tmp/x-pack.zip

# We're going to override the default configuration files.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY config/*               /usr/share/elasticsearch/config/

RUN chmod 700 /*.sh \
    && chmod 700 /usr/share/elasticsearch/config \
    && chmod 644 /usr/share/elasticsearch/config/*

# ENTRYPOINT ["bash"]
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]



