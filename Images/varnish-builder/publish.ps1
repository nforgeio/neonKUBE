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
	)

	$registry = GetRegistry "varnish-builder"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "latest"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -tag $tag
    PushImage "${registry}:$tag"
}

$noImagePush = $nopush

if ($allVersions)
{
}

Build 
