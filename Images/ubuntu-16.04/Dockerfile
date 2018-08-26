#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates an Ubuntu 16.04 image with all current package updates along with some
# additional handy utility packages.
#
# ARGUMENTS:
#
#   TINI_URL        - The [tini] binary URL

FROM        ubuntu:16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         TINI_URL

# Environment

ENV DEBIAN_FRONTEND noninteractive

# Install the [tini] INIT manager.

ADD ${TINI_URL} /sbin/tini
RUN chmod 700 /sbin/tini

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && apt-get update \
    && apt-get install -yq wget curl unzip jq gosu ca-certificates \
    && apt-get install -yq apt-transport-https \
    && apt-get clean -yq

CMD /bin/bash
