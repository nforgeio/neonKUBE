#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
#
# GOLANG image used for building Linux binaries.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   VERSION         - The GOLANG version (e.g. "1.9.4")

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04:${BRANCH}-latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Git and Nano layer

RUN apt-get update \
    && apt-get install -yq git nano \
    && apt-get clean -yq

# Go layer

RUN wget -q -O /tmp/golan.tar.gz https://dl.google.com/go/go${VERSION}.linux-amd64.tar.gz \
    && tar -C /usr/local -xzf /tmp/golan.tar.gz \
    && rm /tmp/golan.tar.gz

ENV PATH $PATH:/usr/local/go/bin

# Go tools layer.
# 
# Note that we're going to temporarily set GOPATH to [/tmp]
# directory so the tools will install there first.  Then 
# we'll copy the binaries to the GO binaries folder.

# $todo(jefflill):
#
# Note also that we're installing the latest [dep] development
# build rather than a specific release version.  We'll probably
# want to change this in the future.

ENV GOPATH /tmp

RUN go get -u github.com/golang/dep/cmd/dep \
    && mv /tmp/bin/* /usr/local/go/bin \
    && rm -r /tmp/*

# Entry point layer.  Note that we're going to set
# [GOPATH=/] because we expect GO projects to be mounted
# at [/src].

ENV GOPATH /

COPY docker-entrypoint.sh   /

RUN chmod 770 /docker-entrypoint.sh

ENTRYPOINT ["/docker-entrypoint.sh"]
