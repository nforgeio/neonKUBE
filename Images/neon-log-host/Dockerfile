#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# TD Agent Bit deployed on each neonKUBE node that forwards log events to the
# cluster log aggregator.

ARG         ORGANIZATION
ARG         CLUSTER_VERSION
FROM        ${ORGANIZATION}/cluster-ubuntu:${CLUSTER_VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         APPNAME

RUN wget -qO - https://packages.fluentbit.io/fluentbit.key | apt-key add - \
    && echo "deb https://packages.fluentbit.io/ubuntu/focal focal main" >> /etc/apt/sources.list \
    && apt update -y \
    && apt install -y td-agent-bit

# Configuration files
COPY conf/td-agent-bit.conf \
     conf/parsers.conf \
     conf/plugins.conf \
     conf/functions.lua \
     conf/td-agent-bit-filter.conf \
     conf/td-agent-bit-input.conf \
     conf/td-agent-bit-output.conf \
     conf/td-agent-bit-service.conf \
     /td-agent-bit/etc/

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
RUN chmod 700 /*.sh

# Entry point
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
