#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the HAProxy images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1 [-all]

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

	$registry = "neoncluster/haproxy"
	$date     = UtcDate
	$tag      = "$version-$date"

	# Build the images.

	./build.ps1 -registry $registry -version $version -tag $tag
    PushImage "${registry}:$tag"

	if ($latest)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:latest" }
		PushImage "${registry}:latest"
	}
}

if ($all)
{
	Build 1.6.9
	Build 1.6.10
	Build 1.7.0
	Build 1.7.1
	Build 1.7.2
}

Build 1.7.8 -latest
