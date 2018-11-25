#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the Varnish images and pushes them to Docker Hub.
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
		[parameter(Mandatory=$True, Position=1)][string] $varnishFamily,    # Varnish version family (like "60")
		[parameter(Mandatory=$True, Position=2)][string] $varnishVersion,   # Specific Varish version (like "6.0.0")
		[switch]$latest = $False
	)

	$registry = GetRegistry "varnish"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$branch-$varnishVersion"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -varnishFamily $varnishFamily -varnishVersion $varnishVersion -tag $tag
    PushImage "${registry}:$tag"

	if (IsProd)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$varnishVersion" }
		PushImage "${registry}:$varnishVersion"

		Exec { docker tag "${registry}:$tag" "${registry}:$varnishVersion-$date" }
		PushImage "${registry}:$varnishVersion-$date"
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
	Build -varnishFamily "60" -varnishVersion "6.0.0"
}

Build -varnishFamily "61" -varnishVersion "6.1.0" -latest
