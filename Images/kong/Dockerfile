#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# neonHIVE Kong AP[I Gateway image.
#
# ARGUMENTS:
#
#   VERSION         - The base Kong image version (e.g. "0.14.0")

ARG         VERSION
FROM        kong:${VERSION}-alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager.

RUN apk add --no-cache tini ca-certificates

# Configure the service.

COPY docker-entrypoint.sh   /neon-docker-entrypoint.sh
COPY _common/*.sh           /

# We're going to rename the original [docker-entrypoint.sh] to
# [kong-entrypoint.sh] and then rename our entrypoint to
# [docker-entrypoint.sh].

RUN chmod 700 /*.sh \
    && mv docker-entrypoint.sh kong-entrypoint.sh \
    && mv neon-docker-entrypoint.sh docker-entrypoint.sh

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
CMD ["kong", "docker-start"]
