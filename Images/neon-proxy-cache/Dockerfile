#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-proxy-cache] service.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   APPNAME         - Name of the application binary/script (e.g "neon-proxy-manager")
#                     This makes it easier to clone and reuse this Dockerfile.
#   VERSION         - The specific [varnish] version (like: "6.0.0")

ARG         ORGANIZATION
ARG         BRANCH
ARG         VERSION
FROM        ${ORGANIZATION}/varnish:${BRANCH}-${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         APPNAME

# Add the application files in two layers.  The first will contain the dependencies
# and the second will contain the application files.  This should result in more
# efficient images because dependencies tend to be more stable than application files.
#
# Note that we used the [core-layers] tool to separate the binary files before
# building the container.

# Dependencies:

COPY bin/__dep              /usr/bin/$APPNAME.dotnet/
RUN echo ** NEW LAYER **

# App files:

COPY bin/__app              /usr/bin/$APPNAME.dotnet/
COPY docker-entrypoint.sh   /

# Handy scripts:

COPY scripts/*.sh           /tmp/scripts/

# Configure the scripts and the service app.

RUN chmod 700 /*.sh \
    && echo "#!/bin/sh" > /usr/bin/$APPNAME \ 
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME \
    && chmod 755 /tmp/scripts/*.sh \
    && cp /tmp/scripts/vadm.sh  /usr/bin/vadm \
    && cp /tmp/scripts/vhist.sh /usr/bin/vhist \
    && cp /tmp/scripts/vlog.sh  /usr/bin/vlog \
    && cp /tmp/scripts/vncsa.sh /usr/bin/vncsa \
    && cp /tmp/scripts/vstat.sh /usr/bin/vstat \
    && cp /tmp/scripts/vtest.sh /usr/bin/vtest \
    && cp /tmp/scripts/vtop.sh  /usr/bin/vtop \
    && rm -r /tmp/scripts

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
