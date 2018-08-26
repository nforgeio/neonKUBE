#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This base image deploys a simple Node service that listens on port 80.

FROM        node:alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /
COPY program.js             /

RUN chmod 700 /docker-entrypoint.sh \
    && apk add --no-cache tini \
    && chmod 700 /sbin/tini \
    && chmod 600 /program.js

EXPOSE 80

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]

