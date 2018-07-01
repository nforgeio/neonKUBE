#------------------------------------------------------------------------------
# Provision and setup the underlying neonHIVE.
#
# usage: powershell -file setup-cluster.ps1 hiveName [OPTIONS]
#
# ARGUMENTS:
#
#   hiveName  		- Identifies the target hive definition in the [clusters]
#	    			  subfolder.  Note that this DOES NOT include the [.json]
#					  file extension).  Example: "wrt-00-prod"
#
# NOTE: This script is not intended to be called directly by a
#       system operator.

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $hiveName
)

if (-not $env:SETUP_ALL -eq "true")
{
	Write-Error "[setup-cluster.ps1]: Is intended to only be called from [setup-all].ps1"
	exit 1
}

# Prepare the neonHIVE.

if ($env:SETUP_SKIP_PREPARE -ne "true")
{
	neon hive prepare `
		$env:SETUP_NOSHIM `
	    $env:SETUP_DEBUG `
		--machine-username="$env:CLUSTER_NODE_TEMPLATE_USERNAME" `
		--machine-password="$env:CLUSTER_NODE_TEMPLATE_PASSWORD" `
		--log-folder="$env:CLUSTER_LOG_FOLDER" `
		--max-parallel=$env:CLUSTER_MAX_PARALLEL `
		$hiveName

	if (-not $?)
	{
		exit 1
	}
}

# Setup the neonHIVE.

if ($env:SETUP_SKIP_SETUP -ne "true")
{
	neon hive setup `
		$env:SETUP_NOSHIM `
	    $env:SETUP_DEBUG `
		$env:SETUP_IMAGE_TAG `
		--machine-username="$env:CLUSTER_NODE_TEMPLATE_USERNAME" `
		--machine-password="$env:CLUSTER_NODE_TEMPLATE_PASSWORD" `
		--log-folder="$env:CLUSTER_LOG_FOLDER" `
		--max-parallel="$env:CLUSTER_MAX_PARALLEL" `
		$env:CLUSTER_LOGIN

	if (-not $?)
	{
		exit 1
	}
}
