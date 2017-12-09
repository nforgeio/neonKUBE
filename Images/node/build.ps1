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
	[parameter(Mandatory=$True,Position=2)][string] $tag,
	[switch]$latest = $False
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

Exec { docker build -t "${registry}:$tag" . }

if ($latest)
{
	Exec { docker tag "${registry}:$tag" "${registry}:latest"}
}

# Clean up

Exec { DeleteFile .rnd }
