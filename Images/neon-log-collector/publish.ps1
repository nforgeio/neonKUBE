#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds all of the supported base [neon-log-collector] images and pushes them 
# to Docker Hub.
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

$registry = "neoncluster/neon-log-collector"

function Build
{
	param
	(
		[parameter(Mandatory=$True, Position=1)][string] $version,    # like: "2017.02.08"
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

# The image tag is the date (YYYY.MM.DD) when the MaxMind.com
# database was released.  We're going to do a HEAD query on the
# database file and extract this from the [Last-Modified] 
# header returned.

$url            = "http://geolite.maxmind.com/download/geoip/database/GeoLite2-City.mmdb.gz";
$request        = [system.Net.HttpWebRequest]::Create($url);
$request.Method = "HEAD";
$response       = $request.GetResponse();
$date           = [System.DateTime]::Parse($response.Headers["Last-Modified"]);
$date           = $date.ToUniversalTime();
$version        = $date.ToString("yyyy.MM.dd");

Build $version -latest
