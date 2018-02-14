#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# GOLANG image used for building Linux binaries.
#
# ARGUMENTS:
#
#   VERSION         - The GOLANG version (e.g. "1.9.4")

FROM        neoncluster/alpine:3.7
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

ENV GOPATH /src

RUN wget -O /tmp/golan.tar.gz https://dl.google.com/go/go${VERSION}.linux-amd64.tar.gz \
    && tar -C /usr/local -xzf /tmp/golan.tar.gz \
    && rm /tmp/golan.tar.gz \
    && apk add --update nano

CMD /bin/sh
