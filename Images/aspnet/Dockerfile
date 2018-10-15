#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Base ASP.NET Core image.
#
# ARGUMENTS:
#
#   VERSION         - The ASP.NET Core image tag (e.g. "2.1")
#
# NOTE:
#
# It appears that that latest point release will be installed when you specify
# only the major and minor version (e.g. spcifying "2.1" will actually install
# "2.1.5" if that's the latest point release).  This means you only need to
# rebuild the image to pick up the latest point release.

ARG         VERSION
FROM        microsoft/dotnet:${VERSION}-aspnetcore-runtime-alpine3.7
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

RUN apk add --no-cache tini ca-certificates libcurl \
    && chmod 700 /*.sh
