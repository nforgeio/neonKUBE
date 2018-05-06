#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# This base image deploys a simple container/or service used
# for neonCLUSTER unit tests.

FROM        neoncluster/alpine
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY docker-entrypoint.sh   /

# We're going to create the [test] user and group with specific IDs for
# unit testing purposes.  Don't change these IDs (UID=5555, GID=6666)
# without also updating the tests to match.

RUN chmod 777 /docker-entrypoint.sh \
    && adduser -u 5555 -S test \
    && addgroup -g 6666 -S test \
    && addgroup test test

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
