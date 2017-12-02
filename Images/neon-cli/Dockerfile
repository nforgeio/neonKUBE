#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-cli].
#
# ARGUMENTS:
#
#       APPNAME     - Name of the application binary/script (e.g "neon")
#                     This makes it easier to clone and reuse this Dockerfile.

FROM        neoncluster/dotnet:2-runtime
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         APPNAME

ENV NEON_TOOL_CONTAINER=1

# Install OpenVPN in it's own layer.

RUN apt-get update \
    && apt-get install -yq openvpn \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

RUN echo ** NEW LAYER **

# Add the application files in two layers.  The first will contain the dependencies
# and the second will contain the application files.  This should result in more
# efficient images because depdencies tend to be more stable than application files.
#
# Note that we used the [core-layers] tool to separate the binary files before
# building the container.

# Dependencies:

COPY bin/__dep              /usr/bin/$APPNAME.dotnet/
RUN echo ** NEW LAYER **

# App files:

COPY bin/__app              /usr/bin/$APPNAME.dotnet/
COPY docker-entrypoint.sh   /

RUN chmod 700 /docker-entrypoint.sh \
    && echo "#!/bin/bash" > /usr/bin/$APPNAME \ 
    && echo "dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME

ENTRYPOINT ["/docker-entrypoint.sh"]
