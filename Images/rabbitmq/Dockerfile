#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# neonHIVE RabbitMQ image (including the management features).
#
# ARGUMENTS:
#
#   VERSION         - The base RabbitMQ image version (e.g. "3.7.7")

ARG         VERSION
FROM        rabbitmq:${VERSION}-management-alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager and rename the original entrypoint script.

RUN apk add --no-cache tini ca-certificates \
    && mv /usr/local/bin/docker-entrypoint.sh /usr/local/bin/rabbitmq-entrypoint.sh

# Configure the service.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

RUN chmod 700 /*.sh

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
