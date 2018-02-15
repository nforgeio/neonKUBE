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

FROM        neoncluster/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Git layer

RUN apt-get update \
    && apt-get install -yq git \
    && apt-get clean -yq

# Go layer

RUN wget -q -O /tmp/golan.tar.gz https://dl.google.com/go/go${VERSION}.linux-amd64.tar.gz \
    && tar -C /usr/local -xzf /tmp/golan.tar.gz \
    && rm /tmp/golan.tar.gz

ENV GOPATH  /src
ENV PATH    $PATH:/usr/local/go/bin

# Go tools layer

RUN go get -u github.com/golang/dep/cmd/dep

# Entry point layer

COPY docker-entrypoint.sh   /

RUN chmod 770 /*.sh

ENTRYPOINT ["/docker-entrypoint.sh"]
