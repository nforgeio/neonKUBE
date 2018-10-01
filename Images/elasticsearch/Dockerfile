#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This is a template of the docker image definition file to be used to generate
# a modified Elasticsearch image based on a specific official version of the
# ElasticSearch image.  This image includes:
#
#   * A custom entry point script that loads environment variables and other
#     settings via the mounted [/etc/neon/host-env] and
#     [/etc/neon/host-env] files.
#
#   * Elasticsearch X-PACK plugins.
#
# ARGUMENTS:
#
#   BASE_IMAGE      - The base OpenJDK image (e.g. "openjdk:10-jre-slim")
#   VERSION         - The Elasticsearch version (e.g. "5.0.1")
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

ARG         BASE_IMAGE
FROM        ${BASE_IMAGE}
MAINTAINER  jeff@lilltek.com
ARG         VERSION

# Install any required packages in a separate layer.  Note that we're
# going to create a symlink to tini at [/sbin/tini] so the ENTRYPOINT
# will be consistent with all of the other Docker images.

RUN apt-get update \
    && apt-get install -yq wget tini gosu \
    && apt-get clean -yq \
    && ln -s /usr/bin/tini /sbin/tini

# Install Elasticsearch to: /usr/share/elasticsearch
#
# Note that Elasticsearch cannot run as [root] so we're going to also
# create the [elasticsearch] user and group.

RUN wget -nv https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-${VERSION}.tar.gz -O /tmp/elasticsearch.tar.gz \
    && tar -xvzf /tmp/elasticsearch.tar.gz --directory /usr/share/ \
    && rm /tmp/elasticsearch.tar.gz \
    && mv /usr/share/elasticsearch-${VERSION} /usr/share/elasticsearch \
    && addgroup elasticsearch \
    && adduser --system --ingroup elasticsearch elasticsearch

# Install X-PACK.

# RUN wget -nv https://artifacts.elastic.co/downloads/packs/x-pack/x-pack-${VERSION}.zip -O /tmp/x-pack.zip \
#    && /usr/share/elasticsearch/bin/elasticsearch-plugin install --batch file:///tmp/x-pack.zip \
#    && rm /tmp/x-pack.zip

# We're going to override the default configuration files.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY config/*               /usr/share/elasticsearch/config/

RUN chmod 700 /*.sh

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
