#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

ARG         ORGANIZATION
ARG         BASE_TAG
FROM        ${ORGANIZATION}/td-agent:${BASE_TAG}
MAINTAINER  marcus@euarctos.com
STOPSIGNAL  SIGTERM
ARG         MAXMIND_KEY

RUN /usr/sbin/td-agent-gem install fluent-plugin-dedot_filter \
    && /usr/sbin/td-agent-gem install fluent-plugin-record-modifier \
    && /usr/sbin/td-agent-gem install fluent-plugin-kubernetes_metadata_filter \
    && /usr/sbin/td-agent-gem install fluent-plugin-elasticsearch \ 
    && /usr/sbin/td-agent-gem install maxminddb \
    && /usr/sbin/td-agent-gem install ipaddress \
    && /usr/sbin/td-agent-gem install browser

RUN mkdir -p /geoip \
    && wget -nv https://download.maxmind.com/app/geoip_download?edition_id=GeoLite2-City\&license_key=${MAXMIND_KEY}\&suffix=tar.gz -O /geoip/database.mmdb.tar.gz

COPY *.sh           /
COPY td-agent.conf  /etc/td-agent/td-agent.conf
COPY plugin/*       /etc/td-agent/plugin/
COPY conf/*         /etc/td-agent/config.d/

RUN chmod 700 /*.sh

# Entry point
ENTRYPOINT ["/docker-entrypoint.sh"]
