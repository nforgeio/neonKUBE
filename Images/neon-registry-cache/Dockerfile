#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# This is a template of the [neon-registry-cache] definition file.
#
# ARGUMENTS:
#
#   VERSION         - The Docker Registry version (e.g. "2.6.0")
#   TINI_VERSION    - The version of the Tini INIT manager to install
#
# REMARKS:
#
# The external Registry data volume must be mounted at:
#
#       /var/lib/registry

ARG         VERSION
FROM        registry:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --update tini

# Install our custom entry point and helper scripts.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY config/*               /

# Set script permissions.

RUN chmod 700 /*.sh

#ENTRYPOINT ["/bin/sleep", "10000"]
ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
