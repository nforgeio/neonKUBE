#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Base Couchbase image.
#
# ARGUMENTS:
#
#   VERSION         - The base COUCHBASE image tag (e.g. "community-5.0.1")

ARG         VERSION
FROM        couchbase/server:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY entrypoint.sh      /
COPY init-cluster.sh    /

RUN chmod 700 /*.sh
