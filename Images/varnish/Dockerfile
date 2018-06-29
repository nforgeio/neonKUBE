#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Base Alpine image.
#
# ARGUMENTS:
#
#   ALPINE_VERSION      - The base ALPINE image tag (e.g. "3.4")
#   VARNISH_VERSION     - The Varnish version

ARG         ALPINE_VERSION
FROM        alpine:${ALPINE_VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VARNISH_VERSION

# Install Varnish

RUN apk add --update varnish=$VARNISH_VERSION
