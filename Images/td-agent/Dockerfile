#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates the base [nhive/td-agent] image used by neonHIVE.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   VERSION         - The TD-AGENT version (e.g. "3")

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Install TD-Agent.

RUN wget -nv https://packages.treasuredata.com/GPG-KEY-td-agent -O /tmp/GPG-KEY-td-agent.key \
    && cat /tmp/GPG-KEY-td-agent.key | apt-key add - \
    && rm /tmp/GPG-KEY-td-agent.key \
    && echo "deb http://packages.treasuredata.com/${VERSION}/ubuntu/xenial/ xenial contrib" > /etc/apt/sources.list.d/treasure-data.list \
    && apt-get update \
    && apt-get install -yq build-essential td-agent \
    && apt-get clean -yq

COPY td-agent.conf /etc/td-agent

RUN chmod 600 /etc/td-agent

ENTRYPOINT ["/sbin/tini", "-g", "--", "/usr/sbin/td-agent"]
