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

RUN chmod 777 /docker-entrypoint.sh \
    && adduser -S test \
    && addgroup -S test \
    && addgroup test test

ENTRYPOINT ["tini", "-g", "--", "/docker-entrypoint.sh"]
