#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported [neon-registry-cache] images and pushes them to Docker Hub.
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

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,
		[switch]$latest = $False
	)

	$registry = "neoncluster/neon-registry-cache"
	$tag      = ImageTag

	# Build the images.

	if (IsProd and $latest)
	{
		./build.ps1 -registry $registry -version $version -tag $tag -latest
	}
	else
	{
		./build.ps1 -registry $registry -version $version -tag $tag 
	}

	PushImage "${registry}:$tag"

	if (IsProd and $latest)
	{
		PushImage "${registry}:latest"
	}
}

Build 2.6.0 -latest
