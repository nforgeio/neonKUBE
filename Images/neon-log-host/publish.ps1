#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill, Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2016-2020 by neonFORGE LLC.  All rights reserved.
#
# Builds the base [neon-log-host] images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1

param 
(
    [switch]$allVersions = $false,
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
		[parameter(Mandatory=$true, Position=1)][string] $version,
		[switch]$latest = $False
	)

	$registry    = GetRegistry "neon-log-host"
	$tag         = $version
	$tagAsLatest = TagAsLatest

	# Build and publish the images.

	. ./build.ps1 -registry $registry -tag $tag
	PushImage "${registry}:$tag"

	if (($latest) -and (TagAsLatest))
	{
		Exec { docker tag "${registry}:$tag" "${registry}:latest" }
		PushImage "${registry}:latest"
	}
}

$noImagePush = $nopush

if ($allVersions)
{
}

Build NeonKubeVersion -latest