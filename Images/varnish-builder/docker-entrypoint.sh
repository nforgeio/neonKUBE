#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Generates a ZIP archive with our slightly modified version of [varnishd].
#
# Before you start if you're using a forked Git repository, be sure to sync it
# with the origin.
#
# Here's how this works:
#
#       1. Clone the request Git source branch.
#       2. Build Varnish from source.
#       3. Create a ZIP file named like: varnish-6.0.0.zip
#       4. Added the [varnishd] binary to the ZIP
#       5. Copy the ZIP to the output folder
#
# To install this, you'll need to:
#
#       1. Use [apt-get] to install the official version of [varnish] (built from the same branch)
#       2. Download the ZIP
#       3. Unzip the archive and then copy [varnish-install/varnishd] to [/usr/local/sbin]

if [ "$1" == "bash "] ; then
    exec bash
fi

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

#------------------------------------------------------------------------------
# Build varnish.  Here's the documentation:
#
#       https://varnish-cache.org/docs/trunk/installation/install.html

# Clone the source repo and switch to the requested branch.

git clone ${GIT_REPO}

cd varnish-cache
git checkout "${GIT_BRANCH}"

# Build Varnish

sh autogen.sh
sh configure
make

#------------------------------------------------------------------------------
# Build the ZIP file.

# Install varnish so we can gather the files.

make install

# Generate and export the ZIP archive.

export OUTPUT_DIR=/varnish-install
mkdir -p $OUTPUT_DIR

mkdir -p $OUTPUT_DIR/usr/local/sbin
cp /usr/local/sbin/varnishd $OUTPUT_DIR/usr/local/sbin

mkdir -p $OUTPUT_DIR/usr/local/bin
cp /usr/local/bin/varnish* $OUTPUT_DIR/usr/local/bin

mkdir -p $OUTPUT_DIR/usr/local/lib/varnish/vmods
cp /usr/local/lib/varnish/vmods/* $OUTPUT_DIR/usr/local/lib/varnish/vmods

export OUTPUT_ZIP=/tmp/varnish-${GIT_BRANCH}.0.zip

zip -r $OUTPUT_ZIP $OUTPUT_DIR/*
cp $OUTPUT_ZIP /mnt/output

#------------------------------------------------------------------------------
# Validate the build if TEST_BUILD=1.  This takes 30+ minutes.

if [ "$TEST_BUILD" == "1" ] ; then
    make check
fi
