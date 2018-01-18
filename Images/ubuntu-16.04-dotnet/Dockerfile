#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Creates an Ubuntu 16.04 image with all current package updates along with some
# additional handy utility packages.
#
# ARGUMENTS:
#
#   VERSION     - The version of the .NET runtime to install (e.g. "2.0.0")

FROM        neoncluster/ubuntu-16.04
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

RUN curl https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor > microsoft.gpg \
    && mv microsoft.gpg /etc/apt/trusted.gpg.d/microsoft.gpg \
    && echo "deb [arch=amd64] https://packages.microsoft.com/repos/microsoft-ubuntu-xenial-prod xenial main" > /etc/apt/sources.list.d/dotnetdev.list \
    && apt-get update \
    && apt-get install -yq dotnet-runtime-$VERSION \
    && apt-get clean -yq

CMD /bin/bash
