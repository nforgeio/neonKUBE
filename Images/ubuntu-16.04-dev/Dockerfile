#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates an Ubuntu 16.04 image with all current package updates along with 
# development related packages including the most recent Linux kernel source
# for Ubuntu 16.04.
#
# ARGUMENTS:
#
#   KERNEL_VERSION      - Linux kernel version for tools and headers (e.g. "4.8.0-58")

FROM        nhive/ubuntu-16.04
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         KERNEL_VERSION

# Environment

ENV DEBIAN_FRONTEND noninteractive

RUN apt-get update \
    && apt-get install -yq build-essential nano \
    && apt-get install -yq linux-headers-${KERNEL_VERSION}-generic \
    && apt-get clean -yq

CMD /bin/bash

# https://blog.sourcerer.io/writing-a-simple-linux-kernel-module-d9dc3762c234
# https://unix.stackexchange.com/questions/397567/kernel-beginner-getting-kernel-source-code-ubuntu
