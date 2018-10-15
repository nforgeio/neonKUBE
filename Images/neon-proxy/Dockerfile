#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# HAProxy image that dynamically loads its configuration from HashiCorp Consul
# and Vault.
#
# ARGUMENTS:
#
#   BRANCH              - The current GitHub branch
#   HAPROXY_VERSION     - HAPROXY version (e.g. "1.6.9")
#   DOTNET_VERSION      - .NET Core versions (e.g. "2.1")
#   APPNAME             - Name of the application binary/script (e.g "neon-proxy")
#                         This makes it easier to clone and reuse the Dockerfile.

ARG         BRANCH
ARG         HAPROXY_VERSION
FROM        haproxy:${HAPROXY_VERSION}-alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         DOTNET_VERSION
ARG         APPNAME

# Install the [tini] INIT manager.

RUN apk add --no-cache tini ca-certificates

# Install the .NET Core dependencies.  This was adapted from:
#
#   https://github.com/dotnet/dotnet-docker/blob/master/2.1/runtime-deps/alpine3.7/amd64/Dockerfile

RUN apk add --no-cache \
        ca-certificates \
        \
        # .NET Core dependencies
        krb5-libs \
        libgcc \
        libintl \
        libssl1.0 \
        libstdc++ \
        tzdata \
        userspace-rcu \
        zlib \
    && apk -X https://dl-cdn.alpinelinux.org/alpine/edge/main add --no-cache \
        lttng-ust

# Configure Kestrel web server to bind to port 80 when present
ENV ASPNETCORE_URLS=http://+:80 \
    # Enable detection of running in a container
    DOTNET_RUNNING_IN_CONTAINER=true \
    # Set the invariant mode since icu_libs isn't included (see https://github.com/dotnet/announcements/issues/20)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

# Install .NET Core.  This was adapted from:
#
#   https://github.com/dotnet/dotnet-docker/blob/master/2.1/runtime/alpine3.7/amd64/Dockerfile

RUN apk add --no-cache --virtual .build-deps openssl \
    && wget -O dotnet.tar.gz https://dotnetcli.blob.core.windows.net/dotnet/Runtime/${DOTNET_VERSION}/dotnet-runtime-${DOTNET_VERSION}-linux-musl-x64.tar.gz \
    && dotnet_sha512='6823778d6ae0a57a9782d1fa460fcea2c7df99c719d14d4aef96e4cbc48406936090e2f727cbcb961f6e645ea960374575e37db8f59907cfc5a588bb1044d840' \
    && echo "$dotnet_sha512  dotnet.tar.gz" | sha512sum -c - \
    && mkdir -p /usr/share/dotnet \
    && tar -C /usr/share/dotnet -xzf dotnet.tar.gz \
    && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
    && rm dotnet.tar.gz \
    && apk del .build-deps

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

# Copy common scripts to the root.

COPY _common/*.sh           /

# Set script permissions, install [bash], [unzip] and [jq] and
# then create the script that launches the application.

RUN chmod 700 /*.sh \
    && apk add --no-cache bash unzip jq \
    && echo "#!/bin/sh" > /usr/bin/$APPNAME \ 
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
