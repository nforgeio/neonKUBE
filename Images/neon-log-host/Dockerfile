#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
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
FROM        ${ORGANIZATION}/fluent-bit:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Configuration files
COPY conf/fluent-bit.conf \
     conf/parsers.conf \
     conf/plugins.conf \
     conf/functions.lua \
     conf/fluent-bit-filter.conf \
     conf/fluent-bit-input.conf \
     conf/fluent-bit-output.conf \
     conf/fluent-bit-service.conf \
     /fluent-bit/etc/

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
RUN chmod 700 /*.sh

# Entry point
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
