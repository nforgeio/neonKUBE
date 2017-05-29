#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds the [neon-cli] images and pushes them to Docker Hub.  This publish
# script is somewhat different than the others because the build script 
# retrives the version tag from the [neon] tool itself and then actually
# publishes the image.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

param 
(
	[switch]$all = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

$registry = "neoncluster/neon-cli"

function Build
{
	param
	(
		[switch]$latest = $False
	)

	# Build the images.

	if ($latest)
	{
		./build.ps1 -latest
	}
	else
	{
		./build.ps1
	}
}

Build -latest
