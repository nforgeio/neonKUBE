#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This image is the default implementation of the [neon-proxy-vault] service responsible
# for forwarding requests to the HashiCorp Vault instances running on the hive's
# manager nodes.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/haproxy:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh \
    && apk add --no-cache curl jq

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
