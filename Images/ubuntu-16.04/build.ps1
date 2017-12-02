#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the base Ubuntu 16.04 image by applying all current package updates to the 
# base Ubuntu image and then adds some handy packages.
#
# Usage: powershell -file build.ps1

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* UBUNTU-16.04"
"======================================="

$registry = "neoncluster/ubuntu-16.04"

Exec { docker build -t "${registry}:latest" --build-arg "TINI_VERSION=$tini_version" . }
