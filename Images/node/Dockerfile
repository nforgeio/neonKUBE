#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This base image deploys a simple Node service that listens on port 80.
#
# ARGUMENTS:
#
#   TINI_VERSION    - The version of the Tini INIT manager to install

FROM        node:alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         TINI_VERSION

COPY docker-entrypoint.sh   /
COPY program.js             /

# $todo(jeff.lill): Shouldn't hardcode the TINI version here.

ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /tini

RUN chmod 700 /docker-entrypoint.sh \
    && chmod 700 /tini \
    && chmod 600 /program.js

EXPOSE 80
#ENTRYPOINT ["sh"]
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
