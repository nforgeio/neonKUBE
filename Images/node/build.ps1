#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds a neonCLUSTER Node.js base image.
#
# Usage: powershell -file build.ps1

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NODE"
"======================================="

# Build the image.

$registry = "neoncluster/node"

Exec { docker build -t "${registry}:latest" --build-arg "TINI_VERSION=$tini_version" . }

