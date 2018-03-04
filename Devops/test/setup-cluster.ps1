#------------------------------------------------------------------------------
# Provision and setup the underlying neonCLUSTER.
#
# usage: powershell -file setup-cluster.ps1 CLUSTER-NAME
#
# ARGUMENTS:
#
#   CLUSTER-NAME		- Identifies the target cluster definition in the [clusters]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "home-small"
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

# Prepare the underlying neonCLUSTER.

neon cluster prepare `
	$clusterName `
	-u="$env:CLUSTER_NODE_TEMPLATE_USERNAME" `
	-p="$env:CLUSTER_NODE_TEMPLATE_PASSWORD" `
	--log-folder="$env:CLUSTER_LOG_FOLDER" `
	--max-parallel="$env:CLUSTER_MAX_PARALLEL"

if (-not $?)
{
	exit 1
}

# Setup the underlying neonCLUSTER.

neon cluster setup `
	-u="$env:CLUSTER_NODE_TEMPLATE_USERNAME" `
	-p="$env:CLUSTER_NODE_TEMPLATE_PASSWORD" `
	--log-folder="$env:CLUSTER_LOG_FOLDER" `
	--max-parallel="$env:CLUSTER_MAX_PARALLEL" `
	"$env:CLUSTER_LOGIN"

if (-not $?)
{
	exit 1
}
