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

# Base YugaByte database image
#
# ARGUMENTS:
#
#   VERSION         - The YugaByte source image tag (like: "2.2.3.0-b35")
#
# NOTE:
#
# It appears that that latest point release will be installed when you specify
# only the major and minor version (e.g. spcifying "2.1" will actually install
# "2.1.5" if that's the latest point release).  This means you only need to
# rebuild the image to pick up the latest point release.

ARG         VERSION
FROM        yugabytedb/yugabyte:${VERSION}
MAINTAINER  jeff@lilltek.com
