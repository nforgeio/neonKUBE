#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported base [neon-log-host] images and pushes them to Docker Hub.
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

$registry = "neoncluster/neon-log-host"

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,    # like: "1.2.3"
		[switch]$latest = $False
	)

	# Build the images.

	if ($latest)
	{
		./build.ps1 -version $version -latest
	}
	else
	{
		./build.ps1 -version $version 
	}

	PushImage "${registry}:$version"

	if ($latest)
	{
		PushImage "${registry}:latest"
	}
}

Build 1.1.0 -latest
