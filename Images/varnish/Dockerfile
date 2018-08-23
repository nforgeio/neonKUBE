#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   FAMILY      - Identifies the major any minor version (like: "60")
#   VERSION     - The specific version (like: "6.0.0")

FROM        nhive/ubuntu-16.04-dotnet
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         FAMILY
ARG         VERSION

RUN apt-get update \
    && apt-get -yq install curl apt-utils nano \
    && curl -s https://packagecloud.io/install/repositories/varnishcache/varnish${FAMILY}/script.deb.sh | bash \
    && apt-get install -yq varnish=${VERSION}\* \
    && apt-get clean -yq
