#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the [neoncluster/tdagent] image.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,    # like: "2"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* TDAGENT " + $version
"======================================="

# Build the images.

$registry = "neoncluster/tdagent";

# Build the images.

$registry = "neoncluster/tdagent"

# Build the image.

Exec { docker build -t "${registry}:$version" --build-arg "VERSION=$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

# Clean up

Exec { DeleteFile .rnd }
