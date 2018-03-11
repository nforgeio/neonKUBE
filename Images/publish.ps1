#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds and publishes all of the Neon Docker images.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish-all.ps1 [-all]

param 
(
	[switch]$all = $False,
    [switch]$nopush = $False
)

#----------------------------------------------------------
# Global includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

# Take care to ensure that you order the image builds such that
# dependant images are built before any dependancies.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Path
    )

	cd "$Path"

	if ($all)
	{
		if ($nopush)
		{
			./publish.ps1 -all -nopush
		}
		else
		{
			./publish.ps1 -all
		}
	}
	else
	{
		if ($nopush)
		{
			./publish.ps1 -nopush
		}
		else
		{
			./publish.ps1
		}
	}
}

# NOTE: 
#
# The build order below is important since later images
# may depend on earlier ones.

Publish "$image_root\\ubuntu-16.04"
Publish "$image_root\\ubuntu-16.04-dotnet"
Publish "$image_root\\alpine"
Publish "$image_root\\golang"
Publish "$image_root\\dotnet"
Publish "$image_root\\openjdk"
Publish "$image_root\\elasticsearch"
Publish "$image_root\\elasticsearch6"
Publish "$image_root\\kibana"
Publish "$image_root\\metricbeat"
Publish "$image_root\\td-agent"
Publish "$image_root\\neon-log-collector"
Publish "$image_root\\neon-log-host"
Publish "$image_root\\node"
Publish "$image_root\\haproxy"
Publish "$image_root\\neon-cli"
Publish "$image_root\\neon-cluster-manager"
Publish "$image_root\\neon-proxy"
Publish "$image_root\\neon-proxy-vault"
Publish "$image_root\\neon-proxy-manager"
Publish "$image_root\\neon-registry"
Publish "$image_root\\neon-registry-cache"
Publish "$image_root\\neon-vegomatic"
Publish "$image_root\\neon-dns"
Publish "$image_root\\neon-dns-health"
