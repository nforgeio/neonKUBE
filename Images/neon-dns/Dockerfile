#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-dns] service.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   APPNAME         - Name of the application binary/script (e.g "neon-dns")
#                     This makes it easier to clone and reuse the Dockerfile.

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/dotnet:${BRANCH}-2.1
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

RUN chmod 700 /docker-entrypoint.sh \
    && echo "#!/bin/sh" > /usr/bin/$APPNAME \ 
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
