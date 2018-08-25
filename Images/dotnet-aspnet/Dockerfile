#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Base .NET Core image (with ASP.NET).
#
# ARGUMENTS:
#
#   VERSION         - The .NET Core image tag (like:"2.1.3-aspnetcore-runtime-alpine3.7")
#   TINI_VERSION    - The version of the Tini INIT manager to install

ARG         VERSION
FROM        microsoft/dotnet:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         TINI_VERSION

# Opt-out of .NET Core telemetry.

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

# Add a file to tweak the DNS resolver's [/etc/resolve.conf] settings.

COPY resolv.conf.head.sh    /etc/resolv.conf.head

# Copy common scripts to the root.

COPY _common/*.sh           /

# Install the [tini] INIT manager.

ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /tini
RUN chmod 700 /tini
