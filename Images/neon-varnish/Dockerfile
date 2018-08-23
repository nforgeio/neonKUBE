#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   VERSION     - The specific [neon-varnish] version (like: "6.0.0")

ARG         VERSION
FROM        nhive/varnish:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh
