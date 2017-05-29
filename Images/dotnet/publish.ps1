#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds the [neoncluster/dotnet] images and pushes them to Docker Hub.
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

$registry = "neoncluster/dotnet"

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,    # like: "1.1.0-runtime"
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

if ($all)
{
	Build 1.0-runtime
	Build 1.0.3-runtime
	Build 1.0.4-runtime
	Build 1.1.0-runtime
	Build 1.1.1-runtime
}

Build 1-runtime
Build 1.1-runtime
Build 1.1.2-runtime