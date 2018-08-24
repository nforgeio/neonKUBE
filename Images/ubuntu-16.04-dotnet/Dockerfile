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
#   VERSION     - The version of the .NET runtime to install (e.g. "2.1")

FROM        nhive/ubuntu-16.04
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
    && apt-get install -yq apt-transport-https apt-utils \
    && apt-get install -yq aspnetcore-runtime-${VERSION} \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get clean -yq

CMD /bin/bash
