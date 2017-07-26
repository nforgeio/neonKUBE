#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Base .NET Core image.
#
# ARGUMENTS:
#
#   VERSION         - The .NET Core image tag (e.g. "1.1.0-runtime")
#   TINI_VERSION    - The version of the Tini INIT manager to install

ARG         VERSION
FROM        microsoft/dotnet:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         TINI_VERSION

# Add a file to tweak the DNS resolver's [/etc/resolve.conf] settings.

COPY resolv.conf.head.sh    /etc/resolv.conf.head

# Install the [tini] INIT manager.

ADD https://github.com/krallin/tini/releases/download/${TINI_VERSION}/tini /tini
RUN chmod 700 /tini
