#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

ARG         ORGANIZATION
ARG         CLUSTER_VERSION
FROM        ${ORGANIZATION}/ubuntu-layer:${CLUSTER_VERSION}
MAINTAINER  marcus@euarctos.com
STOPSIGNAL  SIGTERM
ARG         VERSION


RUN apt update -y -qq && apt install -y -qq gnupg \
    && wget -qO - https://packages.fluentbit.io/fluentbit.key | apt-key add - \
    && echo "deb https://packages.fluentbit.io/ubuntu/focal focal main" >> /etc/apt/sources.list \
    && apt-get update -y -qq \
    && apt-get install -y -qq td-agent-bit=${VERSION} \
    && apt-get clean
