#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the [neon-cli] images and pushes them to Docker Hub.  This publish
# script is somewhat different than the others because the build script 
# retrieves the version tag from the [neon] tool itself and then actually
# publishes the image.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

param 
(
    [switch]$nopush = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[switch]$latest = $False
	)

	# Build the image.

	if ($latest)
	{
		. ./build.ps1 -latest
	}
	else
	{
		. ./build.ps1
	}
}

$noImagePush = $nopush

Build -latest
