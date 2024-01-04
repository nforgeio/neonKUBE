#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
ARG         BASE_IMAGE
FROM        $BASE_IMAGE
MAINTAINER  marcus@neonforge.com
STOPSIGNAL  SIGTERM
ARG         APPNAME

# Environment

ENV TZ=UTC
ENV DEBIAN_FRONTEND noninteractive
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Add the application files in two layers.  The first will contain the dependencies
# and the second will contain the application files.  This should result in more
# efficient images because depdencies tend to be more stable than application files.
#
# Note that we used the [core-layers] tool to separate the binary files before
# building the container.

# Dependencies

COPY bin/__dep              /usr/bin/$APPNAME.dotnet/
RUN echo **NEW LAYER**

# App files.

COPY bin/__app              /usr/bin/$APPNAME.dotnet/
COPY docker-entrypoint.sh   /

RUN chmod 700 /*.sh \
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME \
    && ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone \
    && sed -i 's!^#precedence ::ffff:0:0/96  10$!precedence ::ffff:0:0/96  100!g' /etc/gai.conf

RUN apt-get update -y \
    && apt-get install -y curl \
    && apt-get clean \
    && curl -4fsSL --retry 10 --retry-delay 30 https://dl.min.io/client/mc/release/linux-amd64/mc --output /usr/local/bin/mc \
    && chown -R 1000:1000 /usr/local/bin/mc \
    && chmod 774 /usr/local/bin/mc \
    && mkdir /.mc \
    && chown -R 1000:1000 /.mc

ENTRYPOINT ["/docker-entrypoint.sh"]
