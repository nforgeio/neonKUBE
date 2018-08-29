#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   FAMILY      - Identifies the major/minor version (like: "60")
#   VERSION     - The specific version (like: "6.0.0")

FROM        nhive/ubuntu-16.04-dotnet:2.1
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         FAMILY
ARG         VERSION

# Varnish and dependencies layer.

RUN apt-get update \
    && apt-get install -yq apt-utils \
    && apt-get install -yq curl \
    && curl -s https://packagecloud.io/install/repositories/varnishcache/varnish${FAMILY}/script.deb.sh | bash \
    && apt-get install -yq varnish=${VERSION}\* \
    && apt-get clean -yq

# Entrypoint and other tools layer.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh \
    && apt-get update \
    && apt-get install -yq nano

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
