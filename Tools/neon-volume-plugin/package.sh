#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         package.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# usage:	package.sh PACKAGE-NAME
#
# ARGUMENTS:
#
#	PACKAGE-NAME	- Name of the package including the version.  This
#					  identifies the folder acting as the root of the
#					  Debian file system.
#
# Used by the [package.ps1] script to build the [neon-volume-plugin] Debian
# package from within an Ubuntu Docker container.  This script assumes that
# the PowerShell script has already populated the project's [/bin] folder with
# package folder/files and that the [/bin] folder was mapped to [/src]
# within the container.

packageName=${1}

# We need to copy [/src] to an internal working folder so we can modify 
# the file permissions.  We can't do this directly because [/src] is 
# mapped in from Windows.

mkdir -p /work
cp -r /src/* /work

# Set the package file permissions.

chmod 770 "/work/$packageName/lib/neon/bin/neon-volume-plugin"
chmod 644 "/work/$packageName/lib/systemd/system/neon-volume-plugin.service"

# Build the package.

cd /work
dpkg-deb --build $packageName

# Copy the generated package back to the [/src] file so the workstation
# script will be able to grab it.

cp *.deb /src
