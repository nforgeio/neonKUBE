#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds Varnish and writes the [varnishd] binary to [/mnt/output].

# Add the root directory to the PATH.

PATH=${PATH}:/

# Initialize environment variables.

if [ "${GIT_REPO}" == "" ] ; then
    . log-error.sh "The [GIT_REPO] environment variable is required."
    exit 1
fi

if [ "${GIT_BRANCH}" == "" ] ; then
    . log-error.sh "The [GIT_BRANCH] environment variable is required."
    exit 1
fi

# Ensure that the [/mnt/output] folder exists as a fail-safe.  Normally, the
# caller would mount a host directory here.

mkdir -p /mnt/output

# Clone the source repo and switch to the requested branch.

git clone ${GIT_REPO}

cd varnish-cache
git checkout "${GIT_BRANCH}"

# Build Varnish

sh autogen.sh
sh configure
make

# Validate the build if CHECK=1.  This takes 30+ minutes.

if [ "$CHECK" == "1" ] ; then
    make check
fi

# Copy the output binary.

cp /varnish-cache/bin/varnishd/varnishd /tmp
zip -j /tmp/varnish.${GIT_BRANCH}.0.zip /tmp/varnishd
cp /tmp/varnish.${GIT_BRANCH}.0.zip /mnt/output
