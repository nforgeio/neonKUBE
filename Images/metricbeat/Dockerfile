#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This is a template of the docker image definition file to be used to generate
# the Elastic Metricbeat image to be used to monitor neonHIVE hosts.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   VERSION         - The Metricbeat version (e.g. "5.0.0")

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Install Metricbeat.

RUN wget -nv https://artifacts.elastic.co/downloads/beats/metricbeat/metricbeat-${VERSION}-linux-x86_64.tar.gz -O /tmp/metricbeat.tar.gz \
    && tar -xvzf /tmp/metricbeat.tar.gz --directory /tmp \
    && cp -r /tmp/metricbeat-${VERSION}-linux-x86_64/* / \
    && chmod 700 /metricbeat \
    && cp /metricbeat.yml /metricbeat.yml.org \
    && rm /tmp/metricbeat.tar.gz \
    && rm -r /tmp/metricbeat-${VERSION}-linux-x86_64

COPY config/*               /
COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
CMD ["service"]
