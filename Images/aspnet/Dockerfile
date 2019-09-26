#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
#   VERSION         - The ASP.NET Core image tag (e.g. "2.1-aspnetcore-runtime-alpine3.7")
#
# NOTE:
#
# It appears that that latest point release will be installed when you specify
# only the major and minor version (e.g. spcifying "2.1" will actually install
# "2.1.5" if that's the latest point release).  This means you only need to
# rebuild the image to pick up the latest point release.

ARG         VERSION
FROM        mcr.microsoft.com/dotnet/core/aspnet:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Opt-out of .NET Core telemetry.

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Add a file to tweak the DNS resolver's [/etc/resolve.conf] settings.

COPY resolv.conf.head.sh    /etc/resolv.conf.head

# Copy common scripts to the root.

COPY _common/*.sh           /

# Install the [tini] INIT manager, the CA certificates, and
# [libcurl] so .NET Core apps can use the legacy HttpClientHandler.

ENV TINI_VERSION v0.18.0
ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /sbin/tini

RUN apt-get update -y \
    && apt-get install libcurl4 ca-certificates wget -y \
    && chmod 700 /*.sh \
    && chmod +x /sbin/tini

# Set timezone to UTC
ENV TZ=UTC
