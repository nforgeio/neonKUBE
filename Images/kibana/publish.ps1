#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported Kibana images and pushes them to Docker Hub.
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

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,
		[switch]$latest = $False
	)

	$registry = "neoncluster/kibana"
	$date     = UtcDate
	$tag      = "$version-$date"

	# Build the images.

	if ($latest)
	{
		./build.ps1 -registry $registry -version $version -tag $tag -latest
	}
	else
	{
		./build.ps1 -registry $registry -version $version -tag $tag
	}

    PushImage "${registry}:$tag"

	if ($latest)
	{
		PushImage "${registry}:latest"
	}
}

if ($all)
{
	# Never rebuild 5.2.0 again so it will remain based on the deprecated Kibana image.
	#
	# Build 5.2.0

	Build 5.3.0
	Build 5.4.0
}

Build 5.5.0 -latest
