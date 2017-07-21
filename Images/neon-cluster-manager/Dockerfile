#------------------------------------------------------------------------------
# FILE:         Dockerfile.template
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-cluster-manager].

FROM        neoncluster/dotnet:1.1.2-runtime
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh /
COPY bin                  /usr/bin/neon-cluster-manager.dotnet

RUN chmod 700 /docker-entrypoint.sh \
    && echo "#!/bin/sh" > /usr/bin/neon-cluster-manager \ 
    && echo "dotnet /usr/bin/neon-cluster-manager.dotnet/neon-cluster-manager.dll \"\$@\"" >> /usr/bin/neon-cluster-manager \
    && chmod 770 /usr/bin/neon-cluster-manager \
    && chmod 660 /usr/bin/neon-cluster-manager.dotnet/*

#ENTRYPOINT ["sleep", "100000"]
ENTRYPOINT ["/tini", "-g", "--", "/docker-entrypoint.sh"]
