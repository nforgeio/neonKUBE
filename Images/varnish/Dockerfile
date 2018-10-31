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

# Required packages layer.

RUN apt-get update \
    && apt-get install -yq wget nano \
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
# We'll the munged Debian package with the custom build and install that.  Note 
# that you must be sure to use [varnish-builder] to build the custom package first 
# and upload the archive to S3.  Note that this package will complain about missing
# dependencies when installed.  We'll fix this via: apt-get -f -yq install
#
# Note that we're removing the [/var/log/varnish] directory so we can mount a TMPFS
# at [/var/lib/varnish/_.vsm_mgt] so that the shared memory log won't do a ton of
# wasteful I/O.

ENV VARNISH_HACK 1

RUN wget -nv https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/varnish-${VERSION}.deb -O /tmp/varnish.deb \
    && { dpkg -i /tmp/varnish.deb || true; } \
    && apt-get -f -yq install \
    && rm -r /tmp/varnish.deb \
    && rm -r /var/log/varnish

ENTRYPOINT ["/sbin/tini", "-g", "--", "/docker-entrypoint.sh"]

