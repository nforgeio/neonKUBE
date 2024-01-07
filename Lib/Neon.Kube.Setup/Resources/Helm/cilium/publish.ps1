#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
#
# The contents of this repository are for private use by NEONFORGE, LLC. and may not be
# divulged or used for any purpose by other organizations or individuals without a
# formal written and signed agreement with NEONFORGE, LLC.

param 
(
    [switch]$nopush = $false
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NC_ROOT\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[parameter(Mandatory=$true, Position=1)][string] $version
	)

	$registry = GetKubeBaseRegistry "coredns"
	$tag      = $version

	Log-ImageBuild $registry $tag

	# Build and publish the image.

	. ./build.ps1 -registry $registry -version $version -tag $tag
    Push-ContainerImage "${registry}:$tag"
}

$noImagePush = $nopush

try
{
	Build (Get-KubeVersion CoreDNS)
}
catch
{
	Write-Exception $_
	exit 1
}

