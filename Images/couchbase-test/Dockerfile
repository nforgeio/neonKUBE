#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

# Base Couchbase image.
#
# ARGUMENTS:
#
#   VERSION         - The base COUCHBASE image tag (e.g. "community-5.0.1")

ARG         VERSION
FROM        couchbase/server:${VERSION}
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

COPY entrypoint.sh      /
COPY init-cluster.sh    /

RUN chmod 700 /*.sh

# Set timezone to UTC
ENV TZ=UTC
