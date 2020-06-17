#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
#
# Fluent Bit deployed on each neonKUBE node that forwards log events to the
# cluster log aggregator.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch

ARG         ORGANIZATION
ARG         BRANCH
FROM        gcr.io/google-containers/fluentd-elasticsearch:v2.4.0
MAINTAINER  marcus@marcusbowyer.co.uk
STOPSIGNAL  SIGTERM

ENV TINI_VERSION v0.19.0
ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /sbin/tini
RUN chmod +x /sbin/tini

RUN /usr/local/bin/fluent-gem install fluent-plugin-dedot_filter \
    && /usr/local/bin/fluent-gem install fluent-plugin-record-modifier \
    && /usr/local/bin/fluent-gem install fluent-plugin-kubernetes_metadata_filter \
    && /usr/local/bin/fluent-gem install fluent-plugin-elasticsearch

COPY *.sh           /
COPY _common/*.sh   /
COPY fluent.conf    /etc/fluent
COPY plugin/*       /etc/fluent/plugin/
COPY conf/*         /etc/fluent/config.d/

RUN chmod 700 /*.sh

# Entry point
ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
