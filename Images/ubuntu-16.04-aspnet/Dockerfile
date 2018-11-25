#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
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

# Install .NET Core as described here:
#
#   https://www.microsoft.com/net/learn/get-started/linuxubuntu

RUN wget -q https://packages.microsoft.com/config/ubuntu/16.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb \
    && dpkg -i /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -yq apt-utils \
    && apt-get install -yq apt-transport-https \
    && apt-get install -yq aspnetcore-runtime-${VERSION} \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get clean -yq

CMD /bin/bash
