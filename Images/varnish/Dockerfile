#------------------------------------------------------------------------------
# FILE:         Dockerfile
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# ARGUMENTS:
#
#   BRANCH      - The current GitHub branch
#   FAMILY      - Identifies the major/minor version (like: "61")
#   VERSION     - The specific version (like: "6.1.0")

ARG         BRANCH
FROM        nhive/ubuntu-16.04-dotnet:${BRANCH}-2.1
MAINTAINER  jeff@lilltek.com
STOPSIGNAL  SIGTERM
ARG         FAMILY
ARG         VERSION

# Varnish and dependencies layer.

RUN apt-get update \
    && apt-get install -yq apt-utils \
    && apt-get install -yq curl nano zip \
    && curl -s https://packagecloud.io/install/repositories/varnishcache/varnish${FAMILY}/script.deb.sh | bash \
    && apt-get install -yq varnish=${VERSION}\* \
    && apt-get clean -yq

# Entrypoint and other tools layer.

COPY docker-entrypoint.sh   /
COPY _common/*.sh           /

# Configure the scripts and service app.  Note that we're removing
# the [/var/lib/varnish] directory so we can mount a TMPFS at
# [/var/lib/varnish/_.vsm_mgt] so that the shared memory log won't 
# do a ton of wasteful I/O.

RUN chmod 700 /*.sh \
    && rm -r /var/lib/varnish

# $hack(jeff.lill):
#
# Unfortunately, stock [varnishd] attempts to remove the [/var/lib/varnish/_.vsm_mgt]
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
# We'll download a ZIP file with the custom build and extract it to replace
# version installed above.  Note that you must be sure to use [varnish-builder] 
# to build and ZIP the custom [varnishd] first and upload the archive to S3.

ENV VARNISH_HACK 1

RUN wget -nv https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/varnish.${VERSION}.zip -O /tmp/varnish.zip \
    && cd /tmp \
    && unzip varnish.zip -d . \
    && cp -r ./varnish-install/bin/* /usr/sbin \
    && mkdir -p /usr/local/lib/varnish/vmods \
    && cp -r ./varnish-install/vmods/* /usr/local/lib/varnish/vmods \
    && mkdir -p /usr/local/lib \
    && cp -r ./varnish-install/lib/* /usr/local/lib/ \
    && rm -r /tmp/*

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]

