#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported Ubuntu/.NET Core images and pushes them to Docker Hub.
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
		[parameter(Mandatory=$True, Position=1)][string] $dotnetVersion,
		[switch]$latest = $False
	)

	$registry = GetRegistry "ubuntu-16.04-dotnet"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$branch-$dotnetVersion"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -version $dotnetVersion -tag $tag
    PushImage "${registry}:$tag"

	if (IsProd)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$dotnetVersion" }
		PushImage "${registry}:$dotnetVersion"

		Exec { docker tag "${registry}:$tag" "${registry}:$dotnetVersion-$date" }
		PushImage "${registry}:$dotnetVersion-$date"
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
}

Build 2.1 -latest
