#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported [neon-registry-cache] images and pushes them to Docker Hub.
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
		[parameter(Mandatory=$True, Position=1)][string] $version,
		[switch]$latest = $False
	)

	$registry = "neoncluster/neon-registry-cache"
	$date     = UtcDate
	$branch   = GitBranch

	if (IsProd)
	{
		$tag = "$version-$date"
	}
	else
	{
		$tag = "$branch-$version"
	}

	# Build and publish the images.

	. ./build.ps1 -registry $registry -version $version -tag $tag
	PushImage "${registry}:$tag"

	if (IsProd)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$version" }
		PushImage "${registry}:$version"
	}

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

if ($all)
{
}

Build 2.6.0 -latest
