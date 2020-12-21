#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

# Base ASP.NET Core image.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   CLUSTER_VERSION - The neonKUBE cluster version
#   DOWNLOAD_URL    - URL to the ASPNETCORE runtime download

ARG         ORGANIZATION
ARG         CLUSTER_VERSION
FROM        ${ORGANIZATION}/cluster-ubuntu:${CLUSTER_VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         DOWNLOAD_URL

# Opt-out of .NET Core telemetry.

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Install ASPNETCORE (from https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu)

RUN apt-get update \
    && apt-get install -yq apt-transport-https wget \
    && apt-get clean \
    && wget --quiet -O /tmp/aspnet.tar.gz ${DOWNLOAD_URL} \
    && mkdir -p /lib/dotnet \
    && tar zxf /tmp/aspnet.tar.gz -C /lib/dotnet \
    && rm /tmp/aspnet.tar.gz \
    && echo "export DOTNET_ROOT=/lib/dotnet"  > /etc/bash.bashrc \
    && echo "export PATH=\$PATH:/lib/dotnet" >> /etc/bash.bashrc
