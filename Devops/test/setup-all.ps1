#------------------------------------------------------------------------------
# Configures the TEST workload neonCLUSTER cluster from scratch including provisioning
# the XenServer virtual machines and intializing the pets, databases, and services.
#
# usage: powershell -file setup.ps1 clusterName [imageTag] [OPTIONS]
#
# ARGUMENTS:
#
#   clusterName		    - Identifies the target cluster definition in the [clusters]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "wrt-00-prod"
#
#	imageTag   	    	- Optionally specifies the Docker image tag to use instead
#						  of [latest] when deploying images to the cluster.
#
# OPTIONS:
#
#	-noToolContainer	- Optionally prevents [neon-cli] from shimming into Docker.

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $clusterName,
	[parameter(Mandatory=$False, Position=2)]  [string] $imageTag = "",
    [switch] $noToolContainer
)
	
# Initialize the environment.

cd "$env:NF_ROOT\Devops\test"
./env.ps1 $clusterName -nologin

# Convert the optional parameters into environment variables.

if ($noToolContainer)
{
	$env:SETUP_NO_TOOL_CONTAINER = "--no-tool-container"
}

if ($imageTag -ne "")
{
	$env:SETUP_IMAGE_TAG = "--image-tag=$imageTag"
}

#------------------------------------------------------------------------------
# Provision and setup the base neonCLUSTER.

$env:SETUP_ALL = "true"

neon run --vault-password-file=$env:SECRETS_PASS "$env:SECRETS_VARS" -- `
	powershell -f setup-cluster.ps1 "$env:CLUSTER_SETUP_PATH\clusters\$env:CLUSTER\cluster.json"

if (-not $?)
{
	exit 1
}

#------------------------------------------------------------------------------
# Provision core databases and services.

./setup-core-services.ps1 $clusterName

if (-not $?)
{
	exit 1
}
