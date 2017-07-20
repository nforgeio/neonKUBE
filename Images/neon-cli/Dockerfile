#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-cli].

FROM        neoncluster/dotnet:1.1.2-runtime
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

ENV NEON_TOOL_CONTAINER=1

COPY docker-entrypoint.sh   /
COPY bin                    /usr/bin/neon.dotnet/

RUN chmod 700 /docker-entrypoint.sh \
    && apt-get update \
    && apt-get install -yq openvpn \
    && rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/* \
    && echo "#!/bin/bash" > /usr/bin/neon \ 
    && echo "dotnet /usr/bin/neon.dotnet/neon.dll \"\$@\"" >> /usr/bin/neon \
    && chmod 770 /usr/bin/neon \
    && chmod 660 /usr/bin/neon.dotnet/*

#ENTRYPOINT ["/bin/sh"]
ENTRYPOINT ["/docker-entrypoint.sh"]
