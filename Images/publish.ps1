#------------------------------------------------------------------------------
# FILE:         publish-all.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds and publishes all of the NeonStack Docker images.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish-all.ps1

param 
(
	[switch]$all = $False
)

#----------------------------------------------------------
# Global includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

# Take care to ensure that you order the image builds such that
# dependant images are built before any dependancies.

# $todo(jeff.lill): There's got to be a better way to pass the [-all] parameters.

if ($all)
{
	cd "$image_root\\ubuntu-16.04"
	./publish.ps1 -all

	cd "$image_root\\alpine"
	./publish.ps1 -all

	cd "$image_root\\dotnet"
	./publish.ps1 -all

	cd "$image_root\\elasticsearch"
	./publish.ps1 -all

	cd "$image_root\\kibana"
	./publish.ps1 -all

	cd "$image_root\\metricbeat"
	./publish.ps1 -all

	cd "$image_root\\tdagent"
	./publish.ps1 -all

	cd "$image_root\\neon-log-collector"
	./publish.ps1 -all

	cd "$image_root\\neon-log-host"
	./publish.ps1 -all

	cd "$image_root\\node"
	./publish.ps1 -all

	cd "$image_root\\haproxy"
	./publish.ps1 -all

	cd "$image_root\\neon-cli"
	./publish.ps1 -all

	cd "$image_root\\neon-cluster-manager"
	./publish.ps1 -all

	cd "$image_root\\neon-proxy"
	./publish.ps1 -all

	cd "$image_root\\neon-proxy-vault"
	./publish.ps1 -all

	cd "$image_root\\neon-proxy-manager"
	./publish.ps1 -all

	cd "$image_root\\neon-registry-cache"
	./publish.ps1 -all
}
else
{
	cd "$image_root\\ubuntu-16.04"
	./publish.ps1

	cd "$image_root\\alpine"
	./publish.ps1

	cd "$image_root\\dotnet"
	./publish.ps1

	cd "$image_root\\elasticsearch"
	./publish.ps1

	cd "$image_root\\kibana"
	./publish.ps1

	cd "$image_root\\metricbeat"
	./publish.ps1

	cd "$image_root\\tdagent"
	./publish.ps1

	cd "$image_root\\neon-log-collector"
	./publish.ps1

	cd "$image_root\\neon-log-host"
	./publish.ps1

	cd "$image_root\\node"
	./publish.ps1

	cd "$image_root\\haproxy"
	./publish.ps1

	cd "$image_root\\neon-cli"
	./publish.ps1

	cd "$image_root\\neon-cluster-manager"
	./publish.ps1

	cd "$image_root\\neon-proxy"
	./publish.ps1

	cd "$image_root\\neon-proxy-vault"
	./publish.ps1

	cd "$image_root\\neon-proxy-manager"
	./publish.ps1

	cd "$image_root\\neon-registry-cache"
	./publish.ps1
}
