#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This is a template of the docker image definition file to be used to generate a
# Kibana image based on a specific version of the official Kibana image.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   VERSION         - The Kibana version (e.g. "5.0.0")
#
# REMARKS:
#
# Kibana listens internally on the default port 5601.

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
ARG         VERSION

# Install Kibana

RUN wget -nv https://artifacts.elastic.co/downloads/kibana/kibana-${VERSION}-linux-x86_64.tar.gz -O /tmp/kibana.tar.gz \
    && tar -xvzf /tmp/kibana.tar.gz --directory /usr/share/ \
    && rm /tmp/kibana.tar.gz \
    && mv /usr/share/kibana-${VERSION}-linux-x86_64 /usr/share/kibana

# Install X-PACK.

# RUN wget -nv https://artifacts.elastic.co/downloads/packs/x-pack/x-pack-${VERSION}.zip -O /tmp/x-pack.zip \
#    && cd /usr/share/kibana \
#    && bin/kibana-plugin install file:///tmp/x-pack.zip \
#    && rm /tmp/x-pack.zip

# Overwrite the base Kibana entrypoint script with our own.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY kibana.yml.sh          /usr/share/kibana/config

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
CMD ["service"]
