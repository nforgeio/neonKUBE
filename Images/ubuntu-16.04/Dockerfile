#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Creates an Ubuntu 16.04 image with all current package updates along with some
# additinal handy utility packages.
#
# ARGUMENTS:
#
#   TINI_VERSION    - The version of the Tini INIT manager to install

FROM        ubuntu:16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         TINI_VERSION

# Environment

ENV DEBIAN_FRONTEND noninteractive

# Install the [tini] INIT manager.

ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /tini
RUN chmod 700 /tini

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && apt-get update \
    && apt-get -yq dist-upgrade \
    && apt-get install -yq wget unzip jq gosu \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

CMD /bin/bash
