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
#   VERSION     - HAPROXY version (e.g. "1.6.9")

ARG         VERSION
FROM        haproxy:${VERSION}-alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --no-cache tini

# Install the Consul and Vault binaries.

COPY consul-binaries/*      /usr/local/bin
COPY vault-binaries/*       /usr/local/bin

# Install our custom entry point and helper scripts.

COPY docker-entrypoint.sh   /
COPY onconfigchange.sh      /
COPY report-error.sh        /
COPY logging-loop.sh        /
COPY _common/*.sh           /

# Set script permissions and then install [bash], [unzip] and [jq]. 

RUN chmod 700 /*.sh \
    && apk add --no-cache bash unzip jq

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
