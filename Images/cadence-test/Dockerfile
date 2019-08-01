# Original code: Copyright (c) 2017 Uber Technologies, Inc.
# Modifications: Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
#   VERSION         - The Cadence version to be built (AKA its GitHub tag)
#   GO_VERSION      - The Go version required
#   UI_VERSION      - The Cadence-web version to be built (AKA its GitHub tag)

# Build cadence binaries
ARG GO_VERSION
FROM golang:$GO_VERSION as builder
ARG VERSION

# get and compile cadence-server
RUN go get -u github.com/golang/dep/cmd/dep \
    && go get -u golang.org/x/lint/golint \
    && git clone https://github.com/uber/cadence.git /go/src/github.com/uber/cadence \
    && cd /go/src/github.com/uber/cadence \
    && git checkout tags/$VERSION && make bins_nothrift

# Final Cadence image
# open-jdk-8 debian9 stretch
FROM openjdk:8-jdk-stretch
ARG UI_VERSION

# BASIC COMMON LIBRARY INSTALLATIONS
# CQLSH installation
RUN apt-get update && apt-get install -y --no-install-recommends \
        gettext-base \
        gnupg \
        wget \
        tcpdump \
        netcat \
        python-pip \
        curl \
        python-setuptools \
        build-essential \
    && rm -rf /var/lib/apt/lists/* \
    && pip install wheel \
    && pip install cqlsh

# NODEJS/NPM INSTALLATION
RUN apt-get update \
    && curl -sL https://deb.nodesource.com/setup_10.x | bash - \
    && apt-get install -y nodejs

# CASSANDRA INSTALLATION
RUN apt-get update \
    && echo "deb http://www.apache.org/dist/cassandra/debian 311x main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list \
    && curl https://www.apache.org/dist/cassandra/KEYS | apt-key add - \
    && apt-get update \
    && apt-get install cassandra -y

# Delete default cassandra config file
RUN mkdir /cadence \ 
    && mv /usr/sbin/cassandra /cadence \
    && rm -f /etc/cassandra/cassandra.yaml 

# Clone and build the cadence 
# frontend UI
RUN git clone https://github.com/uber/cadence-web.git /usr/cadence-web \
    && cd /usr/cadence-web \
    && git checkout tags/$UI_VERSION

# Export ENV variable
ENV CADENCE_HOME /cadence
ENV UI_HOME /usr/cadence-web
ENV NODE_ENV=production
ENV TZ=UTC

# Tell to expose ports
EXPOSE 7933 7934 7935 7939 9042 8088

# copy over local cassandra/cadence configuration files, 
# cadence entrypoint script, and 
# cassandra setup script
COPY ./entrypoint.sh $CADENCE_HOME/entrypoint.sh
COPY ./config_template.yaml $CADENCE_HOME/config/docker_template_cassandra.yaml
COPY ./cassandra.yaml /etc/cassandra/cassandra_template.yaml
COPY ./setup.sh $CADENCE_HOME/setup.sh

# copy over necessary cadence files and schemas from the builder
COPY --from=builder /go/src/github.com/uber/cadence/cadence-cassandra-tool /cadence
COPY --from=builder /go/src/github.com/uber/cadence/cadence                /cadence
COPY --from=builder /go/src/github.com/uber/cadence/cadence-server         /cadence
COPY --from=builder /go/src/github.com/uber/cadence/schema/                /cadence/schema/

# change working dir to /usr/cadence-web
# Install cadence ui 
# app dependencies
WORKDIR $UI_HOME
RUN npm install --production \
    && npm run build-production

# change working dir to /cadence
# Change permissions of start scripts 
# Start cassandra and setup schema
WORKDIR $CADENCE_HOME
RUN chmod a+x entrypoint.sh \
    && chmod a+x setup.sh \
    && /bin/bash setup.sh $CADENCE_HOME

# Docker entrypiont
ENTRYPOINT ./entrypoint.sh $CADENCE_HOME $UI_HOME
