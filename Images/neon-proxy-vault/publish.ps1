#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the [neon-proxy-vault] image and pushes it to Docker Hub.
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

$registry = "neoncluster/neon-proxy-vault"

function Build
{
	param
	(
		[switch]$latest = $False
	)

	$registry = "neoncluster/neon-proxy-vault"
	$tag      = ImageTag

	# Build the images.

	if (IsProd and $latest)
	{
		./build.ps1 -registry $registry -tag $tag -latest
	}
	else
	{
		./build.ps1 -registry $registry -tag $tag 
	}

	PushImage "${registry}:$tag"

	if (IsProd and $latest)
	{
		PushImage "${registry}:latest"
	}
}

Build
