#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# CFSSL tool for managing SSL certificates and certificate authorities.
#
# ARGUMENTS:
#
#   VERSION         - The GOLANG version (e.g. "1.9.4")

FROM        nhive/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Git and Nano layer

RUN apt-get update \
    && apt-get install -yq git nano \
    && apt-get clean -yq

# GCC layer (it looks like go/cfssl requires gcc)

RUN apt-get update \
    && apt-get install -yq gcc

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

# $todo(jeff.lill):
#
# Note also that we're installing the latest [dep] development
# build rather than a specific release version.  We'll probably
# want to change this in the future.

ENV GOPATH /tmp
RUN go get -u github.com/golang/dep/cmd/dep

# [cfssl] tools layer

ENV GOPATH /
RUN go get -u github.com/cloudflare/cfssl/cmd/...

# Entrypoint layer

COPY docker-entrypoint.sh   /

RUN chmod 770 /docker-entrypoint.sh

ENTRYPOINT ["/docker-entrypoint.sh"]
