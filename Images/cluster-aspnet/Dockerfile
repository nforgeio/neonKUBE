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
#   DOTNET_VERSION  - The ASP.NET Core version (e.g. "3.1.0")

ARG         ORGANIZATION
ARG         CLUSTER_VERSION
ARG         DOTNET_VERSION
FROM        ${ORGANIZATION}/cluster-alpine:${CLUSTER_VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Opt-out of .NET Core telemetry.

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Install .NET ASPNETCORE v3.1.10 (from https://docs.microsoft.com/en-us/dotnet/core/install/linux-alpine#scripted-install)

RUN apk add --no-cache icu-libs krb5-libs libgcc libintl libssl1.1 libstdc++ zlib \
    && wget -O /tmp/aspnet.tar.gz https://download.visualstudio.microsoft.com/download/pr/43727db2-e279-42cc-b733-f44edfe0b203/3ec40e0dc73278ad0c33c4283026103a/aspnetcore-runtime-3.1.10-linux-musl-x64.tar.gz \
    && mkdir -p /lib/dotnet \
    && tar zxf /tmp/aspnet.tar.gz -C /lib/dotnet \
    && rm /tmp/aspnet.tar.gz \
    && echo "export DOTNET_ROOT=/lib/dotnet" >> /etc/profile \
    && echo "export PATH=\$PATH:/lib/dotnet" >> /etc/profile
