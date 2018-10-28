#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds Varnish and writes a ZIP file including the [varnish*] binaries and modules to [/mnt/output].

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

# Create a local [/output] folder where we'll write output 
# files before we ZIP them.

export OUTPUT_DIR=/varnish-install
mkdir -p $OUTPUT_DIR

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

# Install Varnish so we can gather the files.

make install

# Gather the relevant files to $OUTPUT_DIR.

mkdir -p $OUTPUT_DIR/bin
mkdir -p $OUTPUT_DIR/lib
mkdir -p $OUTPUT_DIR/vmods

cp /varnish-cache/bin/varnishadm/varnishadm     $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishd/varnishd         $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishhist/varnishhist   $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishlog/varnishlog     $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishncsa/varnishncsa   $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishstat/varnishstat   $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishtest/varnishtest   $OUTPUT_DIR/bin
cp /varnish-cache/bin/varnishtop/varnishtop     $OUTPUT_DIR/bin

mkdir -p $OUTPUT_DIR/lib/libvarnish
cp -r /varnish-cache/lib/libvarnishapi/.libs/*  $OUTPUT_DIR/lib/libvarnish

mkdir -p $OUTPUT_DIR/vmods
cp -r /usr/local/lib/varnish/vmods/*            $OUTPUT_DIR/vmods

#------------------------------------------------------------------------------
# ZIP and publish the output files.

export OUTPUT_ZIP=/tmp/varnish.${GIT_BRANCH}.0.zip

zip $OUTPUT_ZIP $OUTPUT_DIR/bin/*
zip $OUTPUT_ZIP $OUTPUT_DIR/vmods/*
zip $OUTPUT_ZIP $OUTPUT_DIR/lib/*

cp $OUTPUT_ZIP /mnt/output

#------------------------------------------------------------------------------
# Validate the build if TEST_BUILD=1.  This takes 30+ minutes.

if [ "$TEST_BUILD" == "1" ] ; then
    make check
fi
