#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported Elasticsearch images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1 [-all]

param 
(
	[switch]$allVersions = $False,
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
		[parameter(Mandatory=$True, Position=2)][string] $baseImage,
		[switch]$latest = $False
	)

	$registry = GetRegistry "elasticsearch"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$branch-$version"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -baseImage $baseImage -version $version -tag $tag
    PushImage "${registry}:$tag"

	if (IsProd)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$version" }
		PushImage "${registry}:$version"

		Exec { docker tag "${registry}:$tag" "${registry}:$version-$date" }
		PushImage "${registry}:$version-$date"
	}

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

if ($allVersions)
{
	Build 6.1.1 openjdk:9-jre-slim
}

Build 6.4.1 openjdk:10-jre-slim -latest
