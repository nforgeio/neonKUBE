#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonCLUSTER Node.js base image.
#
# Usage: powershell -file build.ps1

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NODE " + $tag
"======================================="

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "TINI_VERSION=$tini_version" . }

# Clean up

Exec { DeleteFile .rnd }
