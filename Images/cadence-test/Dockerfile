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

# Image is Debian:jessie image
#
# ARGUMENTS:
#
#   VERSION         - The Cadence version to be built (AKA its GitHub branch)
#   GO_VERSION      - The Go version required

# Build cadence binaries
ARG GO_VERSION
FROM golang:$GO_VERSION as builder
ARG VERSION

# get and compile cadence-server
RUN go get -u github.com/golang/dep/cmd/dep \
    && go get -u golang.org/x/lint/golint \
    && git clone https://github.com/nforgeio/cadence.git /go/src/github.com/nforgeio/cadence \
    && cd /go/src/github.com/nforgeio/cadence \
    && git checkout $VERSION && make bins_nothrift

# Final Cadence image
# open-jdk-8 debian9 stretch
FROM openjdk:8u212-jdk-stretch

# BASIC COMMON LIBRARY INSTALLATIONS
# CQLSH installation
RUN apt-get update && apt-get install -y --no-install-recommends \
        gettext-base \
        wget \
        vim \
        tcpdump \
        netcat \
        python-pip \
        mysql-client \
        curl \
        python-setuptools \
    && rm -rf /var/lib/apt/lists/* \
    && pip install wheel \
    && pip install cqlsh

# CASSANDRA INSTALLATION
RUN apt-get update \
    && echo "deb http://www.apache.org/dist/cassandra/debian 311x main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list \
    && curl https://www.apache.org/dist/cassandra/KEYS | apt-key add - \
    && apt-get update \
    && apt-get install cassandra -y

# Delete bad cassandra config file
RUN mkdir /cadence \ 
    && mv /usr/sbin/cassandra /cadence \
    && rm -f /etc/cassandra/cassandra.yaml 

# Export CADENCE_HOME ENV variable
ENV CADENCE_HOME /cadence

# Set timezone to UTC
ENV TZ=UTC

# Tell to expose ports
EXPOSE 7933 7934 7935 7939 9042

# copy over local cassandra/cadence configuration files, 
# cadence entrypoint script, and 
# cassandra setup script
COPY ./entrypoint.sh $CADENCE_HOME/entrypoint.sh
COPY ./config_template.yaml $CADENCE_HOME/config/docker_template_cassandra.yaml
COPY ./cassandra.yaml /etc/cassandra/cassandra_template.yaml
COPY ./setup.sh $CADENCE_HOME/setup.sh

# copy over necessary cadence files and schemas from the builder
COPY --from=builder /go/src/github.com/nforgeio/cadence/cadence-cassandra-tool /cadence
COPY --from=builder /go/src/github.com/nforgeio/cadence/cadence                /cadence
COPY --from=builder /go/src/github.com/nforgeio/cadence/cadence-server         /cadence
COPY --from=builder /go/src/github.com/nforgeio/cadence/schema/                /cadence/schema/

# change working dir to /cadence
WORKDIR $CADENCE_HOME

# Change permissions of start scripts 
# Start cassandra and setup schema
RUN chmod a+x entrypoint.sh \
    && chmod a+x setup.sh \
    && /bin/bash setup.sh $CADENCE_HOME

# Docker entrypiont
ENTRYPOINT ./entrypoint.sh $CADENCE_HOME
