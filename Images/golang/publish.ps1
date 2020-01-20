#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
#
# Builds the GOLANG build images and pushes them to Docker Hub.
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
		[switch]$latest = $False
	)

	$registry = GetRegistry "golang"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$branch-$version"

	# Build and publish the images.

	. ./build.ps1 -registry $registry -version $version -tag $tag
    PushImage "${registry}:$tag"

	if (IsRelease)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$version" }
		PushImage "${registry}:$version"

		Exec { docker tag "${registry}:$tag" "${registry}:$version-$date" }
		PushImage "${registry}:$version-$date"
	}

	if ($latest)
	{
		if (TagAsLatest)
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
    # 1.10.* releases:

    Build 1.10
    Build 1.10.1
    Build 1.10.2
    Build 1.10.3
    Build 1.10.4
    Build 1.10.5
    Build 1.10.6
    Build 1.10.7
    Build 1.10.8

    # 1.11.* releases:

    Build 1.11
    Build 1.11.1
    Build 1.11.2
    Build 1.11.3
    Build 1.11.4
    Build 1.11.5
    Build 1.11.6
    Build 1.11.7
    Build 1.11.8
    Build 1.11.9
    Build 1.11.10
    Build 1.11.11
    Build 1.11.12

    # 1.12.* releases:

    Build 1.12
    Build 1.12.1
    Build 1.12.2
    Build 1.12.3
    Build 1.12.4
    Build 1.12.5
    Build 1.12.6
}
    
Build 1.12.7 -latest
