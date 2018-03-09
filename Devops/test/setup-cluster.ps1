#------------------------------------------------------------------------------
# Provision and setup the underlying neonCLUSTER.
#
# usage: powershell -file setup-cluster.ps1 clusterName [OPTIONS]
#
# ARGUMENTS:
#
#   clusterName  		- Identifies the target cluster definition in the [clusters]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "wrt-00-prod"
#
# NOTE: This script is not intended to be called directly by a
#       system operator.

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $clusterName
)

if (-not $env:SETUP_ALL -eq "true")
{
	Write-Error "[setup-cluster.ps1]: Is intended to only be called from [setup-all].ps1"
	exit 1
}

# Prepare the neonCLUSTER.

if ($env:SETUP_SKIP_PREPARE -ne "true")
{
	neon cluster prepare `
		$env:SETUP_NO_TOOL_CONTAINER `
		--machine-username="$env:CLUSTER_NODE_TEMPLATE_USERNAME" `
		--machine-password="$env:CLUSTER_NODE_TEMPLATE_PASSWORD" `
		--log-folder="$env:CLUSTER_LOG_FOLDER" `
		--max-parallel=$env:CLUSTER_MAX_PARALLEL `
		$clusterName

	if (-not $?)
	{
		exit 1
	}
}

# Setup the neonCLUSTER.

if ($env:SETUP_SKIP_SETUP -ne "true")
{
	neon cluster setup `
		$env:SETUP_NO_TOOL_CONTAINER `
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
