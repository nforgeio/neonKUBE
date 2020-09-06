#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
#
# Builds a Docker image with the AWS-CLI tool.  This image boots to Bash
# so you can easily enter commands manually.
#
# NOTE: This is considered a development-only tool and is never pushed
#       to the public [nkubeio] organization.

FROM        ubuntu:20.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM

# This prevents [apt-get] from prompting for the timezone when installing
# the required packages.

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update -y \
    && apt-get install -yq unzip curl less groff \
    && curl https://awscli.amazonaws.com/awscli-exe-linux-x86_64.zip -o awscliv2.zip \
    && unzip awscliv2.zip \
    && ./aws/install \
    && rm -r aws \
    && rm awscliv2.zip \
    && apt-get clean

ENTRYPOINT ["/usr/bin/bash"]
