#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This is a template of the [neon-registry-cache] definition file.
#
# ARGUMENTS:
#
#   VERSION         - The Docker registry version (e.g. "2.6.0")
#
# REMARKS:
#
# The external registry data volume must be mounted at:
#
#       /var/lib/registry

ARG         VERSION
FROM        registry:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --no-cache tini ca-certificates

# Install our custom entry point and helper scripts.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /
COPY config/*               /

# Set script permissions and install [mini-httpd] to
# install the [htpasswd] tool from [gitolite].  This
# is the only Alpine package I could find that has this.

RUN chmod 700 /*.sh

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
