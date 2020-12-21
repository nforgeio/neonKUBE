#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

# Base Ubuntu image.
#
# ARGUMENTS:
#
#   IMAGE_DIGEST    - The base ubuntu image DIGEST (e.g. "ubuntu@sha256:4e4bc990609ed865e07afc8427c30ffdddca5153fd4e82c20d8f0783a291e241")

ARG         IMAGE_DIGEST
FROM        ${IMAGE_DIGEST}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Add a file to tweak the DNS resolver's [/etc/resolve.conf] settings.

COPY resolv.conf.head.sh    /etc/resolv.conf.head

# Set timezone to UTC

ENV TZ=UTC

# Install TINI, CA certificates, and other commonly useful packages.

RUN apt-get update \
    && apt-get install -yq tini ca-certificates nano \
    && apt-get clean
