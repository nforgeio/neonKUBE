# Copyright (c) 2017 Uber Technologies, Inc.
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

# Base golang image.
#
# ARGUMENTS:
#
#   VERSION         - The base golang image tag (e.g. "latest")
#   GIT_BRANCH      - Specify the cadence version to be built into this docker image (See https://github.com/uber/cadence/tags)

# Build cadence binaries
FROM golang:latest as builder

ARG GIT_BRANCH
ARG VERSION
RUN if [ -z "$GIT_BRANCH" ]; then echo "ERROR: git_branch NOT SET. Usage: docker build . --build-arg git_branch=YOUR_CHECKOUT_BRANCH"; exit 1; else : ; fi

# get and compile cadence-server
RUN go get -u github.com/golang/dep/cmd/dep \
    && go get -u golang.org/x/lint/golint \
    && git clone https://github.com/uber/cadence.git /go/src/github.com/uber/cadence \
    && cd /go/src/github.com/uber/cadence \
    && git checkout $git_branch && make bins_nothrift


# Final Cadence image
FROM debian:jessie

# BASIC COMMON LIBRARY INSTALLATIONS
RUN apt-get update && apt-get install -y --no-install-recommends \
        gettext-base \
        wget \
        vim \
        tcpdump \
        netcat \
        python-pip \
        mysql-client \
        curl \
        software-properties-common -y \
    && rm -rf /var/lib/apt/lists/*

# CQLSH INSTALLATION
RUN pip install cqlsh

## JAVA INSTALLATION
RUN apt-get update \
    && echo "oracle-java8-installer shared/accepted-oracle-license-v1-1 select true" | debconf-set-selections \
    && echo "deb http://ppa.launchpad.net/webupd8team/java/ubuntu trusty main" > /etc/apt/sources.list.d/webupd8team-java-trusty.list \
    && apt-key adv --keyserver keyserver.ubuntu.com --recv-keys EEA14886 \
    && apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --force-yes --no-install-recommends oracle-java8-installer \
    && apt-get clean all

# CASSANDRA INSTALLATION
RUN apt-get update \
    && echo "deb http://www.apache.org/dist/cassandra/debian 311x main" | tee -a /etc/apt/sources.list.d/cassandra.sources.list \
    && curl https://www.apache.org/dist/cassandra/KEYS | apt-key add - \
    && apt-key adv --keyserver pool.sks-keyservers.net --recv-key A278B781FE4B2BDA \
    && apt-get update \
    && apt-get install cassandra -y

# Delete bad cassandra config file
RUN mkdir /cadence \ 
    && mv /usr/sbin/cassandra /cadence \
    && rm -f /etc/cassandra/cassandra.yaml 

# Export CADENCE_HOME ENV variable
ENV CADENCE_HOME /cadence

# Tell to expose ports
EXPOSE 7933 7934 7935 7939

COPY ./start.sh $CADENCE_HOME/start.sh
COPY ./config_template.yaml $CADENCE_HOME/config/docker_template_cassandra.yaml
COPY ./cassandra.yaml /etc/cassandra/cassandra_template.yaml

# Change permissions of start script
RUN chmod a+x $CADENCE_HOME/start.sh

# copy over necessary cadence files and schemas from the builder
COPY --from=builder /go/src/github.com/uber/cadence/cadence-cassandra-tool /cadence
COPY --from=builder /go/src/github.com/uber/cadence/cadence /cadence
COPY --from=builder /go/src/github.com/uber/cadence/cadence-server /cadence
COPY --from=builder /go/src/github.com/uber/cadence/schema/ /cadence/schema/

# change working dir to /cadence
WORKDIR $CADENCE_HOME

# execute start script
CMD /bin/bash entrypoint.sh $CADENCE_HOME
