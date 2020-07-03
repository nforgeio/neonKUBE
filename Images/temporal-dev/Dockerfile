# Original code: Copyright (c) 2017 Uber Technologies, Inc.
# Modifications: Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.

# Image is openjdk:8-jdk-stretch image
# base debain9 stretch
#
# ARGUMENTS:
#
#   VERSION         - The Temporal version to be built (AKA its GitHub tag)
#   GO_VERSION      - The Go version required
#   UI_VERSION      - The Temporal-web version to be built (AKA its GitHub tag)

# Build temporal binaries

ARG  GO_VERSION
FROM golang:$GO_VERSION as builder
ARG  VERSION

# get and compile temporal-server

COPY ./.gitmodules_temporal /

RUN apt-get update && apt-get install -y protobuf-compiler \
    && git clone https://github.com/temporalio/temporal.git /go/src/github.com/temporalio/temporal \
    && rm -f /go/src/github.com/temporalio/temporal/.gitmodules \ 
    && cd /go/src/github.com/temporalio/temporal \
    && git checkout tags/$VERSION \
    && cp /.gitmodules_temporal /go/src/github.com/temporalio/temporal/.gitmodules 

WORKDIR /go/src/github.com/temporalio/temporal
RUN CGO_ENABLED=0 make install
    
# Download dockerize

FROM alpine:3.11 AS dockerize

RUN apk add --no-cache openssl

ENV DOCKERIZE_VERSION v0.6.1
RUN wget https://github.com/jwilder/dockerize/releases/download/$DOCKERIZE_VERSION/dockerize-alpine-linux-amd64-$DOCKERIZE_VERSION.tar.gz \
    && tar -C /usr/local/bin -xzvf dockerize-alpine-linux-amd64-$DOCKERIZE_VERSION.tar.gz \
    && rm dockerize-alpine-linux-amd64-$DOCKERIZE_VERSION.tar.gz \
    && echo "**** fix for host id mapping error ****" \
    && chown root:root /usr/local/bin/dockerize

# Build Temporal UI

FROM node:8.17.0-jessie as temporal-web-builder
ARG UI_VERSION
WORKDIR /usr
ENV NODE_ENV=production
ENV NPM_CONFIG_PRODUCTION=true

COPY ./.gitmodules_temporal_web /

# Clone and build the temporal 
# frontend UI

RUN git clone https://github.com/temporalio/temporal-web.git \
    && cd temporal-web \
    && git checkout $UI_VERSION \
    && rm -f .gitmodules \
    && cp /.gitmodules_temporal_web .gitmodules

WORKDIR /usr/temporal-web    
RUN make

# Install app dependencies
# build the app
# move all app files into ./app

WORKDIR /usr/temporal-web
RUN npm install --production \
    && npm run build-production \
    && mkdir app && mv package.json server.js webpack.config.js server dist node_modules temporal-proto ./app \
    && mkdir -p ./app/protobuf/src && mv protobuf/src ./app/protobuf

# Ubuntu-16.04 base image

FROM nkubedev/ubuntu-16.04 AS ubuntu
ENV NODE_VERSION=8.x

# BASIC COMMON LIBRARY INSTALLATIONS
# CQLSH installation

RUN apt-get update && apt-get install -y --no-install-recommends \
        gettext-base \
        openjdk-8-jdk \
        python2.7 \
        python-pip \
        python-setuptools \
    && rm -rf /var/lib/apt/lists/* \
    && pip install wheel \
    && pip install cqlsh

# cassandra installation

RUN apt-get update \
    && echo "deb https://downloads.apache.org/cassandra/debian 311x main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list \
    && curl https://downloads.apache.org/cassandra/KEYS | apt-key add - \
    && apt-get update \
    && apt-get install cassandra -y \
    && rm -f /etc/cassandra/cassandra.yaml

# NODEJS/NPM INSTALLATION

RUN apt-get update \
    && curl -sL https://deb.nodesource.com/setup_$NODE_VERSION | bash - \
    && apt-get install -y nodejs

# set up nsswitch.conf for Go's "netgo" implementation
# https://github.com/gliderlabs/docker-alpine/issues/367#issuecomment-424546457
RUN test ! -e /etc/nsswitch.conf & echo 'hosts: files dns' > /etc/nsswitch.conf

SHELL ["/bin/bash", "-c"]

# Final Temporal image

FROM ubuntu as temporal-dev
ENV TEMPORAL_HOME /etc/temporal
ENV UI_HOME /etc/temporal-web
ENV TZ=UTC
ENV NODE_ENV=production

RUN mkdir $TEMPORAL_HOME \ 
    && mkdir $UI_HOME

# copy over necessary temporal files and schemas from the builder

COPY --from=dockerize /usr/local/bin/dockerize                                     /usr/local/bin
COPY --from=builder /go/src/github.com/temporalio/temporal/temporal-cassandra-tool /usr/local/bin
COPY --from=builder /go/src/github.com/temporalio/temporal/temporal-sql-tool       /usr/local/bin
COPY --from=builder /go/src/github.com/temporalio/temporal/tctl                    /usr/local/bin
COPY --from=builder /go/src/github.com/temporalio/temporal/temporal-server         /usr/local/bin
COPY --from=builder /go/src/github.com/temporalio/temporal/schema/                 $TEMPORAL_HOME/schema/
COPY --from=temporal-web-builder /usr/temporal-web/app/                            $UI_HOME

# copy over local cassandra/temporal configuration files, 
# temporal entrypoint script, and 
# cassandra setup script

COPY ./entrypoint.sh        /docker-entrypoint.sh
COPY ./config_template.yaml $TEMPORAL_HOME/config/config_template.yaml
COPY ./cassandra.yaml       /etc/cassandra/cassandra_template.yaml
COPY ./setup.sh             /setup.sh
COPY ./start.sh             /start.sh
COPY ./start-temporal.sh    /start-temporal.sh
COPY ./start-temporal-ui.sh /start-temporal-ui.sh

# change working dir to /cadence
# Change permissions of start scripts 
# Start cassandra and setup schema

RUN chmod a+x docker-entrypoint.sh \
    && chmod a+x setup.sh \
    && /bin/bash setup.sh $TEMPORAL_HOME

ENV SERVICES="history,matching,frontend,worker"

# Docker entrypiont

EXPOSE 7233 7234 7235 7239 9042 8088
ENTRYPOINT ["/docker-entrypoint.sh"]
CMD /start.sh
