#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Generates a Debian package with our slightly modified version of [varnishd].
#
# Before you start if you're using a forked Git repository, be sure to sync it
# with the origin.
#
# Here's how this works:
#
#       1. Clone the request Git source branch.
#       2. Build Varnish from source.
#       3. Download the official Varnish package
#       4. Unpack the package to a directory
#       5. Replace the original [varnishd] with our new one
#       6. Modify the package service by appending "-neonFORGE"
#       7. Rebuild the package and copy it to the output folder
#
# These steps were inspired by:
#
#       https://unix.stackexchange.com/questions/138188/easily-unpack-deb-edit-postinst-and-repack-deb

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

if [ "${PACKAGE_VERSION}" == "" ] ; then
    . log-error.sh "The [PACKAGE_VERSION] environment variable is required."
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
# Here's where we do the [varnishd] organ replacement in the standard Debian
# package.  We're going to assume that Varnish-Cache is still using the
# http://packagecloud.io service to host their packages by assuming the URL
# will look like:
#
#       https://packagecloud.io/varnishcache/varnish61/ubuntu/pool/xenial/main/v/varnish/varnish_6.1.1-1~xenial_amd64.deb
#
# To figure this out, I manually configured the Varnish repository using this
# packagecloud.io script in another container:
#
#       curl -s https://packagecloud.io/install/repositories/varnishcache/varnish61/script.deb.sh | bash
#
# and then ran this command:
#
#       apt-get install varnish -V | grep "Get:1"
#
# This prints out something like:
#
#       Get:1 https://packagecloud.io/varnishcache/varnish61/ubuntu xenial/main amd64 varnish amd64 6.1.1-1~xenial [2632 kB]
#
# Which has everything you'll need to manually construct the URI (following the convention above).
#
# The trick is that the "61" must match the desired release version (without the revision part).

# Create a temporary working directory.

mkdir -p /tmp/varnish-package
cd /tmp/varnish-package

# Download the official package.  Note that [packagecloud.io] will perform a few redirects,
# probably for billing/tracking purposes.  We're also going to strip the period out of the
# GIT_BRANCH to obtain the VARNISH_FAMILY.

VARNISH_FAMILY=$(echo ${GIT_BRANCH} | sed 's/\.//')
curl -4fsSLv https://packagecloud.io/varnishcache/varnish${VARNISH_FAMILY}/ubuntu/pool/xenial/main/v/varnish/varnish_${PACKAGE_VERSION}-1~xenial_amd64.deb > official.deb

# Unpack the official varnish package.

mkdir -p ./unpacked
dpkg-deb -R official.deb ./unpacked

# Copy the custom [varnishd] binary into the unpacked package.

cp /varnish-cache/bin/varnishd/varnishd ./unpacked/usr/sbin/varnishd

# $hack(jeff.lill):
#
# We need to update the version in the package control file to avoid potential conflicts.
# We're simply going to replace "xenial" with "xenial-neonFORGE" as a bit of a hack.
#
# NOTE: This will need to be changed if we ever upgrade the containers to another distribution.

sed 's/xenial/xenial-neonFORGE/' ./unpacked/DEBIAN/control > new-control
cp new-control ./unpacked/DEBIAN/control

# Repackage the modified archive and put it in the output directory.

dpkg-deb -b ./unpacked /mnt/output/varnish-${PACKAGE_VERSION}.deb

#------------------------------------------------------------------------------
# Validate the build if TEST_BUILD=1.  This takes 30+ minutes.

if [ "$TEST_BUILD" == "1" ] ; then
    make check
fi
