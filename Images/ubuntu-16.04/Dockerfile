#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

# Set timezone to UTC
ENV TZ=UTC

# Install the [tini] INIT manager.

ADD ${TINI_URL} /sbin/tini
RUN chmod 700 /sbin/tini

# NOTE: The first command modifies DNS to query for IPv4 before IPv6.

RUN sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf \
    && apt-get update \
    && apt-get install -yq --allow-downgrades wget curl unzip jq gosu ca-certificates \
    && apt-get install -yq --allow-downgrades apt-transport-https \
    && apt-get clean -yq

CMD /bin/bash
