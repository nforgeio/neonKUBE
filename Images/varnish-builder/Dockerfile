#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Used to build a slighlty customized version of Varnish.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization

ARG         ORGANIZATION
FROM        ${ORGANIZATION}/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Build dependencies and utilities layer.

RUN apt-get update \
    && apt-get install -yq nano git zip aptitude \
    && apt-get install -yq make automake autotools-dev libedit-dev libjemalloc-dev libncurses-dev libpcre3-dev libtool pkg-config python-docutils python-sphinx \
    && apt-get install -yq graphviz autoconf-archive \
    && apt-get clean -yq

# Entrypoint and other tools layer.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

# Configure the scripts.

RUN chmod 700 /*.sh

ENTRYPOINT ["/docker-entrypoint.sh"]
