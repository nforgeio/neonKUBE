#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This is a template of the docker image definition file to be used to generate a
# Kibana image based on a specific version of the official Kibana image.
#
# ARGUMENTS:
#
#   VERSION     - The Kibana version (e.g. "5.0.0")
#
# REMARKS:
#
# Kibana listens internally on the default port 5601.

ARG         VERSION
FROM        kibana:${VERSION}
MAINTAINER  jeff@lilltek.com
ARG         VERSION

# Environment

ENV DEBIAN_FRONTEND noninteractive

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && apt-get update \
    && apt-get install -yq wget \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

# Install X-PACK.

RUN wget -nv https://artifacts.elastic.co/downloads/packs/x-pack/x-pack-${VERSION}.zip -O /tmp/x-pack.zip \
    && cd /usr/share/kibana \
    && bin/kibana-plugin install file:///tmp/x-pack.zip \
    && rm /tmp/x-pack.zip

# Overwrite the base Kibana entrypoint script with our own.

COPY docker-entrypoint.sh   /
copy kibana.yml.sh          /etc/kibana

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && chmod 700 /docker-entrypoint.sh \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

ENTRYPOINT [ "/docker-entrypoint.sh", "kibana" ]

