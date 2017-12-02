#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Creates the base [neoncluster/tdagent] image used by neonCLUSTER.
#
# ARGUMENTS:
#
#   VERSION         - The TD-AGENT version (e.g. "2")

FROM        neoncluster/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Install TD-Agent.

RUN wget -nv https://packages.treasuredata.com/GPG-KEY-td-agent -O /tmp/GPG-KEY-td-agent.key \
    && cat /tmp/GPG-KEY-td-agent.key | apt-key add - \
    && rm /tmp/GPG-KEY-td-agent.key \
    && echo "deb http://packages.treasuredata.com/2/ubuntu/xenial/ xenial contrib" > /etc/apt/sources.list.d/treasure-data.list \
    && apt-get update \
    && apt-get install -yq td-agent \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

COPY td-agent.conf /etc/td-agent

RUN chmod 600 /etc/td-agent

ENTRYPOINT [ "/tini", "-g", "--", "/usr/sbin/td-agent" ]
# ENTRYPOINT [ "/bin/bash" ]
