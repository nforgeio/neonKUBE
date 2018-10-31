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

	$registry = "nhive/varnish"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$varnishVersion-$date"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -varnishFamily $varnishFamily -varnishVersion $varnishVersion -tag $tag
    PushImage "${registry}:$tag"

	Exec { docker tag "${registry}:$tag" "${registry}:$branch-$varnishVersion" }
	PushImage "${registry}:$branch-$varnishVersion"

	if (IsProd)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$varnishVersion" }
		PushImage "${registry}:$varnishVersion"
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
	Build "60" "6.0.1"
}

Build "61" "6.1.1" -latest
