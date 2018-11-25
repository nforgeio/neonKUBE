#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This base image deploys a simple container/or service used
# for neonHIVE unit tests.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/alpine:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /

RUN chmod 777 /docker-entrypoint.sh

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
