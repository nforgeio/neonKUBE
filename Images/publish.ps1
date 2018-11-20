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
	[switch]$all         = $False,        # Rebuild all images
	[switch]$base        = $False,        # Rebuild base images
	[switch]$dotnetBase  = $False,        # Rebuild .NET base images
	[switch]$dotnet      = $False,        # Rebuild .NET based images
	[switch]$other       = $False,        # Rebuild all other images (usually script based)
    [switch]$nopush      = $False,        # Don't push to the registry
    [switch]$noprune     = $False,        # Don't prune the local Docker state
	[switch]$allVersions = $False         # Rebuild all image versions
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

	if ($allVersions)
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

# Handle the command line arguments.

if ($all)
{
	$base       = $True
	$dotnetBase = $True
	$dotnet     = $True
	$other      = $True
}
elseif ((-not $base) -and (-not $dotnet) -and (-not $other))
{
	# Build .NET and other images, but not base images, 
	# by default.

	$dotnet = $True
	$other  = $True
}

# Purge any local Docker images as well as the image build cache.
# This also purges everything else Docker as a side effect.  We
# need to do this to ensure that we get a clean build.

if (-not $noprune)
{
	docker system prune -af
}

# NOTE: 
#
# The build order below is important since later images
# may depend on earlier ones.

if ($base)
{
	$dotnetBase = $True

	# Base OS images:

	Publish "$image_root\\alpine"
	Publish "$image_root\\ubuntu-16.04"

	# Other base images:

	Publish "$image_root\\golang"
	Publish "$image_root\\elasticsearch"
	Publish "$image_root\\kibana"
	Publish "$image_root\\metricbeat"
	Publish "$image_root\\kong"
	Publish "$image_root\\td-agent"
	Publish "$image_root\\node"
	Publish "$image_root\\haproxy"
	Publish "$image_root\\neon-registry"
	Publish "$image_root\\neon-registry-cache"
}

if ($dotnetBase)
{
	Publish "$image_root\\dotnet"
	Publish "$image_root\\aspnet"
	Publish "$image_root\\ubuntu-16.04-dotnet"
	Publish "$image_root\\ubuntu-16.04-aspnet"
	Publish "$image_root\\varnish"
}

if ($dotnet)
{
	Publish "$image_root\\neon-cli"
	Publish "$image_root\\neon-hive-manager"
	Publish "$image_root\\neon-dns"
	Publish "$image_root\\neon-dns-mon"
	Publish "$image_root\\neon-proxy"
    Publish "$image_root\\neon-proxy-cache"
	Publish "$image_root\\neon-proxy-manager"
	Publish "$image_root\\neon-secret-retriever"
    Publish "$image_root\\vegomatic"
}

if ($other)
{
	Publish "$image_root\\neon-log-collector"
	Publish "$image_root\\neon-log-host"
	Publish "$image_root\\neon-proxy-vault"
	Publish "$image_root\\neon-hivemq"
	Publish "$image_root\\couchbase-test"
	Publish "$image_root\\rabbitmq-test"
	Publish "$image_root\\test"
	Publish "$image_root\\varnish-builder"
}
