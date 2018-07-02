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
		[parameter(Mandatory=$True, Position=1)][string] $alpineVersion,
		[parameter(Mandatory=$True, Position=2)][string] $varnishVersion,
		[switch]$latest = $False
	)

	$registry = "nhive/varnish"
	$date     = UtcDate
	$branch   = GitBranch

	# $todo(jeff.lill):
	#
	# We're currently ignoring [$varnishVersion] when setting the image label.
	# In the future, [build.ps1] should actually download the Varnish source
	# for the specified version and build it.

	if (IsProd)
	{
		# $tag = "$varnishVersion-$date"
		$tag = "$date"
	}
	else
	{
		# $tag = "$branch-$varnishVersion"
		$tag = "$branch-$date"
	}

	# Build and publish the images.

	. ./build.ps1 -registry $registry -alpineVersion $alpineVersion -varnishVersion $varnishVersion -tag $tag
    PushImage "${registry}:$tag"

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

Build "3.7" "5.2.1-r0" -latest
