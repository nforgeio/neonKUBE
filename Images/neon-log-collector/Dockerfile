#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# Fluent Bit deployed on each neonKUBE node that forwards log events to the
# cluster log aggregator.

ARG         ORGANIZATION
ARG         CLUSTER_VERSION
FROM        ${ORGANIZATION}/cluster-ubuntu:${CLUSTER_VERSION}
STOPSIGNAL  SIGTERM
ARG         APPNAME
ARG         MAXMIND_KEY

RUN curl https://packages.treasuredata.com/GPG-KEY-td-agent | apt-key add - \
    && echo "deb http://packages.treasuredata.com/4/ubuntu/focal/ focal contrib" > /etc/apt/sources.list.d/treasure-data.list \
    && apt update -y \
    && apt install -y td-agent gcc ruby-dev rubygems make g++

RUN /usr/sbin/td-agent-gem install fluent-plugin-dedot_filter \
    && /usr/sbin/td-agent-gem install fluent-plugin-record-modifier \
    && /usr/sbin/td-agent-gem install fluent-plugin-kubernetes_metadata_filter \
    && /usr/sbin/td-agent-gem install fluent-plugin-elasticsearch \
    && /usr/sbin/td-agent-gem install maxminddb \
    && /usr/sbin/td-agent-gem install ipaddress \
    && /usr/sbin/td-agent-gem install browser

RUN mkdir -p /geoip \
    && wget -nv https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City\&license_key=${MAXMIND_KEY}\&suffix=tar.gz -O /geoip/database.mmdb.tar.gz

COPY *.sh           /
COPY _common/*.sh   /
COPY fluent.conf /etc/fluent/fluent.conf
COPY plugin/*       /etc/fluent/plugin/
COPY conf/*         /etc/fluent/config.d/


RUN chmod 700 /*.sh

# Entry point
ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
