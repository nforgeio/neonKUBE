#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   ORGANIZATION    - The Docker Hub organization
#   BRANCH      - The current GitHub branch
#   FAMILY      - Identifies the major/minor version (like: "61")
#   VERSION     - The specific version (like: "6.1.0")

ARG         ORGANIZATION
ARG         BRANCH
FROM        ${ORGANIZATION}/ubuntu-16.04-dotnet:${BRANCH}-2.1
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         FAMILY
ARG         VERSION

# Required packages layer.

RUN apt-get update \
    && apt-get install -yq wget nano zip curl \
    && apt-get install -yq gnupg apt-transport-https \
    && apt-get clean -yq

# Entrypoint and other tools layer.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

# Configure the scripts and service app.

RUN chmod 700 /*.sh

# $hack(jeff.lill):
#
# Unfortunately, the stock [varnishd] tries to remove the [/var/lib/varnish/_.vsm_mgt]
# directory when it starts but this will fail because it needs to be mounted
# as a TMPFS.  We don't actually need the directory to be removed because 
# we know that its already empty when the container starts.
#
# The hack is to replace stock [varnishd] with a custom version that ignores
# the two lines of code that clear and then create the [/var/lib/varnish/_.vsm_mgt]
# directory when the VARNISH_HACK environment variable is defined.  We'll also need 
# to use the [varnishd -n /var/lib/varnish] option so that Varnish won't create a 
# subfolder with an instance ID and so we'll have a stable target location where we
# can mount the TMPFS.
#
# We'll install the official Varnish version and then download a ZIP archive with
# the munged build and extract it to overwrite the official version.
#
# Note that we're removing the [/var/log/varnish] directory so we can mount a TMPFS
# at [/var/lib/varnish/_.vsm_mgt] so that the shared memory log won't do a ton of
# wasteful I/O.

ENV VARNISH_HACK 1

RUN curl -L https://packagecloud.io/varnishcache/varnish${FAMILY}/gpgkey | apt-key add - \
    && echo "deb https://packagecloud.io/varnishcache/varnish${FAMILY}/ubuntu/ xenial main" > /etc/apt/sources.list.d/varnishcache_varnish${FAMILY}.list \
    && echo "deb-src https://packagecloud.io/varnishcache/varnish${FAMILY}/ubuntu/ xenial main" >> /etc/apt/sources.list.d/varnishcache_varnish${FAMILY}.list \
    && apt-get update \
    && apt-get install -yq varnish=${VERSION}-1~xenial \
    && wget -nv https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/varnish-${VERSION}.zip -O /tmp/varnish.zip \
    && unzip /tmp/varnish.zip -d /tmp \
    && cd /tmp/varnish-install \
    && mkdir -p /usr/local/bin \
    && mkdir -p /usr/local/sbin \
    && mkdir -p /usr/lib/varnish/vmods \
    && mkdir -p /usr/local/lib/varnish/vmods \
    && cp ./usr/local/bin/*               /usr/local/bin \
    && cp ./usr/local/sbin/*              /usr/local/sbin \
    && cp ./usr/local/lib/varnish/vmods/* /usr/lib/varnish/vmods \
    && cp ./usr/local/lib/varnish/vmods/* /usr/local/lib/varnish/vmods \
    && rm -r /tmp/varnish.zip \
    && rm -r /tmp/varnish-install \
    && rm -r /var/log/varnish \
    && apt-get clean -yq

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]
