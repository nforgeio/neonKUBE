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
#   ORGANIZATION    - The Docker Hub organization
#   VERSION         - The version of the .NET runtime to install (e.g. "2.1")
#
# NOTE:
#
# It appears that that latest point release will be installed when you specify
# only the major and minor version (e.g. spcifying "2.1" will actually install
# "2.1.5" if that's the latest point release).  This means you only need to
# rebuild the image to pick up the latest point release.

ARG         ORGANIZATION
FROM        ${ORGANIZATION}/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Environment

ENV DEBIAN_FRONTEND noninteractive

# Opt-out of .NET Core telemetry.

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Set timezone to UTC
ENV TZ=UTC

# Install .NET Core as described here:
#
#   https://www.microsoft.com/net/learn/get-started/linuxubuntu

RUN wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -yq --allow-downgrades apt-utils \
    && apt-get install -yq --allow-downgrades apt-transport-https \
    && apt-get install -yq --allow-downgrades dotnet-runtime-${VERSION} \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get clean -yq

CMD /bin/bash
