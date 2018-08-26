#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Base Alpine image.
#
# ARGUMENTS:
#
#   VERSION         - The base ALPINE image tag (e.g. "3.4")

ARG         VERSION
FROM        alpine:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --no-cache tini ca-certificates
