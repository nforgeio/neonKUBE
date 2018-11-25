#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# .NET Core based image that implements the [neon-cli].
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH          - The current GitHub branch
#   APPNAME         - Name of the application binary/script (e.g "neon")
#                     This makes it easier to clone and reuse this Dockerfile.

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04-dotnet:${BRANCH}-2.1
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         APPNAME

# The environment variable indicates to [neon-cli] that it's running in a container.

ENV NEON_TOOL_CONTAINER=1

# We're going to configure NANO as the default editor used by Ansible Vault
# to edit encrypted files.  We're also going to install VIM/VI as an option.

ENV EDITOR=/bin/nano

RUN apt-get update \
    && apt-get install -yq nano vim zip \
    && apt-get clean -yq

# Install OpenVPN in it's own layer.

RUN apt-get update \
    && apt-get install -yq openvpn \
    && apt-get clean -yq

# [neon-cli] implements commands that integrate Ansible and since Ansible
# only runs on Linux, we're going to integrate it into this image.
#
#   http://docs.ansible.com/ansible/latest/intro_installation.html#latest-releases-via-apt-ubuntu

RUN apt-get update \
    && apt-get install -yq software-properties-common \
    && apt-add-repository ppa:ansible/ansible \
    && apt-get update \
    && apt-get install -yq ansible \
    && apt-get clean -yq

# Add the application files in two layers.  The first will contain the dependencies
# and the second will contain the application files.  This should result in more
# efficient images because dependencies tend to be more stable than application files.
#
# Note that we used the [core-layers] tool to separate the binary files before
# building the container.

RUN echo ** NEW LAYER **

# Dependencies:

COPY bin/__dep              /usr/bin/$APPNAME.dotnet/
RUN echo ** NEW LAYER **

# App files:

COPY bin/__app              /usr/bin/$APPNAME.dotnet/
COPY docker-entrypoint.sh   /
COPY conf/ssh_config.conf   /etc/ssh/ssh_config
COPY modules/*              /usr/share/ansible/plugins/modules/

# External Ansible modules.  Both of these have pull requests pending to
# have them built-in to Ansible.
#
# https://github.com/dariko/ansible-module-docker_stack
# https://github.com/dariko/ansible/tree/ansible-modules-core/pull/5547/lib/ansible/modules/cloud/docker

# The Docker 

RUN chmod 700 /docker-entrypoint.sh \
    && chmod 666 /etc/ssh/ssh_config \
    && echo "#!/bin/bash" > /usr/bin/$APPNAME \ 
    && echo "exec dotnet /usr/bin/$APPNAME.dotnet/$APPNAME.dll \"\$@\"" >> /usr/bin/$APPNAME \
    && chmod 770 /usr/bin/$APPNAME \
    && chmod 770 /usr/share/ansible/plugins/modules/*

ENTRYPOINT ["/docker-entrypoint.sh"]
