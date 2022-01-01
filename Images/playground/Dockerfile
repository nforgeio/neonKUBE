#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

# This image is intended to be deployed within a Kubernetes cluster to
# be EXEC'ed into to be able to manually play around with cluster 
# resources from the inside.
#
# This image includes bash, nano, curl, net-tools, tcpdump etc.

ARG         VERSION
FROM        alpine:latest
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

RUN apk update && \
   apk add bash nano curl net-tools tcpdump

ENTRYPOINT ["sleep", "365d"]
