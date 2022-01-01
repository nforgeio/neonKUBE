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

# Base NATS-STREAMING image.
#
# ARGUMENTS:
#
#   VERSION         - The source NATS image version (e.g. "1.4.1")

ARG         VERSION
FROM        nats:${VERSION}-linux
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# Expose client, management, and cluster ports
EXPOSE 4222 8222 6222
