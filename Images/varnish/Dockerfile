#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   BRANCH      - The current GitHub branch
#   FAMILY      - Identifies the major/minor version (like: "60")
#   VERSION     - The specific version (like: "6.0.0")

ARG         BRANCH
FROM        nhive/ubuntu-16.04-dotnet:${BRANCH}-2.1
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         FAMILY
ARG         VERSION

# Varnish and dependencies layer.

RUN apt-get update \
    && apt-get install -yq apt-utils \
    && apt-get install -yq curl nano \
    && curl -s https://packagecloud.io/install/repositories/varnishcache/varnish${FAMILY}/script.deb.sh | bash \
    && apt-get install -yq varnish=${VERSION}\* \
    && apt-get clean -yq

# Entrypoint and other tools layer.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

# Configure the scripts and service app.  Note that we're removing
# the [/var/lib/varnish] directory so we can mount a TMPFS at
# [/var/lib/varnish/_.vsm_mgt] so that the shared memory log won't 
# do a ton of wasteful I/O.

RUN chmod 700 /*.sh \
    && rm -r /var/lib/varnish

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
