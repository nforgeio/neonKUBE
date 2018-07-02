#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# TD-Agent deployed on each neonHIVE node that forwards log events to the
# cluster log aggregator.

FROM        nhive/td-agent
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Expose the standard TD-Agent network ports for TCP/UDP
# and HTTP.

EXPOSE 24224/tcp 24224/udp 9880/tcp

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY td-agent.conf          /etc/td-agent

RUN chmod 700 /*.sh \
    && chmod 600 /etc/td-agent \
    && /usr/sbin/td-agent-gem install fluent-plugin-systemd -v 0.1.0 --no-document

ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
