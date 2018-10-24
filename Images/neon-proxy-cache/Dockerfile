#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-proxy-cache] service.
#
# ARGUMENTS:
#
#       BRANCH      - The current GitHub branch
#       APPNAME     - Name of the application binary/script (e.g "neon-proxy-manager")
#                     This makes it easier to clone and reuse this Dockerfile.
#       VERSION     - The specific [varnish] version (like: "6.0.0")

ARG         BRANCH
ARG         VERSION
FROM        nhive/varnish:${BRANCH}-${VERSION}
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

# Configure the scripts and service app.  Note that we're removing
# the [/var/lib/varnish] directory so we can mount a TMPFS there
# so that the shared memory log won't do a ton of wasteful I/O.

RUN chmod 700 /*.sh \
    && echo "#!/bin/sh" > /usr/bin/$APPNAME \ 
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME \
    && rm -r /var/lib/varnish

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
