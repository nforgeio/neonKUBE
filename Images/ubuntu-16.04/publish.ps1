#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the base Ubuntu 16.04 image and pushes it to Docker Hub.
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

$registry = "neoncluster/ubuntu-16.04"

function Build
{
	param
	(
		[switch]$latest = $False
	)

	$registry = "neoncluster/ubuntu-16.04"
	$tag      = ImageTag

	# Build the images.

	if ($latest)
	{
		./build.ps1 -registry $registry -tag $tag -latest
	}
	else
	{
		./build.ps1 -registry $registry $version -tag $tag
	}

    PushImage "${registry}:$tag"

	if ($latest)
	{
		PushImage "${registry}:latest"
	}
}

Build -latest

