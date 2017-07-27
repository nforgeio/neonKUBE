#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# HAProxy image that loads its configuration from a mounted file.
#
# ARGUMENTS:
#
#   VERSION         - The HAProxy image tag (e.g. "1.6.9")

ARG         VERSION
FROM        haproxy:${VERSION}-alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --update tini

# Configure the service.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh

ENTRYPOINT ["tini","-g","--","/docker-entrypoint.sh"]
