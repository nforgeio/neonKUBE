#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# [neon-hivemq] image (including the management features).
#
# ARGUMENTS:
#
#   VERSION         - The base RabbitMQ image version (e.g. "3.7.7")

ARG         VERSION
FROM        rabbitmq:${VERSION}-management
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Install the [tini] INIT manager, the latest certificates, and [pwgen] to we can generate
# the Erlang cookie if necessary.  We're also going to rename the original entrypoint script
# and replace it with our own script which does some stuff and then calls the original.
#
# Note that we also need to munge the original RabbitMQ entrypoint script a bit so that
# it recognizes all of our environment variables.  This is a bit fragile:
#
#   Port Variables
#   --------------
#    5671 --> $RABBITMQ_NODE_PORT
#    5672 --> $RABBITMQ_NODE_PORT
#   15671 --> $RABBITMQ_MANAGEMENT_PORT
#   15672 --> $RABBITMQ_MANAGEMENT_PORT
#
# Note that we need to replace the longer strings first.

RUN apt-get update \
    && apt-get install -yq tini pwgen ca-certificates \
    && apt-get clean -yq \
    && cp /usr/local/bin/docker-entrypoint.sh /rabbitmq-entrypoint.sh.shipped \
    && cp /usr/local/bin/docker-entrypoint.sh /rabbitmq-entrypoint.sh \
    && sed -i 's/15671/$RABBITMQ_MANAGEMENT_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/15672/$RABBITMQ_MANAGEMENT_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/5671/$RABBITMQ_NODE_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/5672/$RABBITMQ_NODE_PORT/g' /rabbitmq-entrypoint.sh

# Configure the service.

COPY docker-entrypoint.sh   /
COPY rabbitmq-entrypoint.sh /
COPY set-cluster-name.sh    /
COPY _common/*.sh           /

RUN chmod 777 /*.sh \
    && sed -i 's/15671/$RABBITMQ_MANAGEMENT_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/15672/$RABBITMQ_MANAGEMENT_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/5671/$RABBITMQ_NODE_PORT/g' /rabbitmq-entrypoint.sh \
    && sed -i 's/5672/$RABBITMQ_NODE_PORT/g' /rabbitmq-entrypoint.sh

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
