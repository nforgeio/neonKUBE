#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# TD-Agent deployed as the [neon-log-collector] service to receive events
# from the node [neon-log-host] containers, perform any transformations
# and then persist them to Elasticsearch.

FROM        neoncluster/tdagent
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Expose the standard TDAgent network ports for TCP/UDP
# and HTTP.

EXPOSE 24224/tcp 24224/udp 9880/tcp

# Install required Ruby gems and the Nano editor because it's
# handy for debugging.

RUN /usr/sbin/td-agent-gem install fluent-plugin-elasticsearch --no-document \
    && /usr/sbin/td-agent-gem install fluent-plugin-record-modifier --no-document \
    && /usr/sbin/td-agent-gem install browser --no-document \
    && /usr/sbin/td-agent-gem install maxminddb --no-document \
    && apt-get update \
    && apt-get install -yq nano \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

# Download the MAXMIND geolocation database.

RUN mkdir -p /geoip \
    && wget -nv http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz -O /geoip/database.mmdb.gz

# Configure custom scripts, templates and plugins.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY td-agent.conf          /etc/td-agent
COPY plugin/*               /etc/td-agent/plugin/
COPY geoip/                 /geoip/
COPY template/*             /etc/td-agent/template/

RUN chmod 700 /*.sh \
    && chmod 600 /etc/td-agent

#ENTRYPOINT ["sleep", "100000"]
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
