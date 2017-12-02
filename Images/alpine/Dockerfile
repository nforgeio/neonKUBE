#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# HAProxy image that loads its configuration from a mounted file.
#
# ARGUMENTS:
#
#   VERSION         - The HAProxy image tag (e.g. "1.6.9")

ARG         VERSION
FROM        alpine:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --update tini
