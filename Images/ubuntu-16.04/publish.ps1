#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the base Ubuntu 16.04 image and pushes it to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1 [-all]

param 
(
	[switch]$all = $False,
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

	$registry = "neoncluster/ubuntu-16.04"
	$tag      = UtcDate
	$branch   = GitBranch

	# Build and publish the images.

	. ./build.ps1 -registry $registry -tag $tag
	PushImage "${registry}:$tag"

	if ($latest)
	{
		if (IsProd)
		{
			Exec { docker tag "${registry}:$tag" "${registry}:latest" }
			PushImage "${registry}:latest"
		}
		else
		{
			Exec { docker tag "${registry}:$tag" "${registry}:${branch}-latest" }
			PushImage "${registry}:${branch}-latest"
		}
	}
}

$noImagePush = $nopush

Build -latest

