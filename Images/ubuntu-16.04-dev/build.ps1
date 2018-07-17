#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the development Ubuntu 16.04 image.
#
# Usage: powershell -file build.ps1 REGISTRY TAG
 
param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

"   "
"======================================="
"* UBUNTU-16.04-DEV"
"======================================="

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "KERNEL_VERSION=4.8.0-58" . }

if ($latest)
{
	Exec { docker tag "${registry}:$tag" "${registry}:latest" }
}
