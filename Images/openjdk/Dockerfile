#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates an Ubuntu 16.04 image with Java OpenJDK installed.
#
# ARGUMENTS:
#
#       VERSIONS    - The JDK version (like: "8")

FROM        nhive/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         VERSION

# Install OpenJDK

RUN apt-get update \
    && apt-get install -yq openjdk-${VERSION}-jre-headless \
    && apt-get clean -yq
