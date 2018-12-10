#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the [neon-proxy] image and pushes them to Docker Hub.
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
		[parameter(Mandatory=$True, Position=1)][string] $haproxyVersion,
		[parameter(Mandatory=$True, Position=2)][string] $dotnetVersion,
		[switch]$latest = $False
	)

	$registry = GetRegistry "neon-proxy"
	$tag      = ImageTag
	$branch   = GitBranch

	# Build and publish the images.

	. ./build.ps1 -registry $registry -haProxyVersion $haproxyVersion -dotnetVersion $dotnetVersion -tag $tag
    PushImage "${registry}:$tag"

	if ($latest)
	{
		if (IsProd)
		{
			Exec { docker tag "${registry}:$tag" "${registry}:latest" }
			PushImage "${registry}:latest"
		}

        Exec { docker tag "${registry}:$tag" "${registry}:${branch}-latest" }
		PushImage "${registry}:${branch}-latest"
	}
}

$noImagePush = $nopush

# Note that we need to pass the full .NET Core version
# (e.g. "2.1.5" instead of just "2.1").

Build -haProxyVersion 1.8.14 -dotnetVersion 2.1.5 -latest
