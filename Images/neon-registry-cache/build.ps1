#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds a neonCLUSTER [neon-registry-cache] image with the specified version, subversion
# and majorversion.  The image built will be a slightly modified version of the 
# Docker Registry reference.
#
# Usage: powershell -file build.ps1 VERSION [SUBVERSION] [MAJORVERSION] [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,                # like: "5.0.0"
	[parameter(Mandatory=$False,Position=2)][string] $subversion = "-",      # like: "5.0"
	[parameter(Mandatory=$False,Position=3)][string] $majorversion = "-",    # like: "5"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-REGISTRY-CACHE " + $version
"======================================="

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

# Build the images.

$registry = "neoncluster/neon-registry-cache"

Exec { docker build -t "${registry}:$version" --build-arg "VERSION=$version" --build-arg "TINI_VERSION=$tini_version" . }

if ($subversion -ne "-")
{
	Exec { docker tag "${registry}:$version" "${registry}:$subversion"}
}

if ($majorversion -ne "-")
{
	Exec { docker tag "${registry}:$version" "${registry}:$majorversion"}
}

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

# Cleanup

sleep 5 # Docker sometimes appears to hold references to the files below for a bit.

Exec { Remove-Item -Recurse _common }
