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
#	-noshim	            - Optionally prevents [neon-cli] from shimming into Docker.
#
#	-skipPrepare		- Skip the cluster prepare step.
#
#	-skipSetup			- Skip the cluster setup step.
#
#	-skipCoreServices	- Skip deploying core services (like databases).
#						  This implies setting [-skipServices].
#
#	-skipServices		- Skip deploying cluster services.

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $clusterName,
	[parameter(Mandatory=$False, Position=2)]  [string] $imageTag = "",
    [switch] $noshim           = $False,
    [switch] $skipPrepare      = $False,
    [switch] $skipSetup        = $False,
    [switch] $skipCoreServices = $False,
	[switch] $skipServices     = $False
)
	
# Initialize the environment.

cd "$env:NF_ROOT\Devops\test"
./env.ps1 $clusterName -nologin

# Convert the optional parameters into environment variables.

if ($noshim)
{
	$env:SETUP_NOSHIM = "--noshim"
}

if ($imageTag -ne "")
{
	$env:SETUP_IMAGE_TAG = "--image-tag=$imageTag"
}

if ($skipPrepare)
{
	$env:SETUP_SKIP_PREPARE = "true"
}

if ($skipSetup)
{
	$env:SETUP_SKIP_SETUP = "true"
}

if ($skipCoreServices)
{
	$env:SETUP_SKIP_CORE_SERVICES = "true"
	$skipServices                 = $True
}

if ($skipServices)
{
	$env:SETUP_SKIP_SERVICES = "true"
}

#------------------------------------------------------------------------------
# Provision and setup the base neonCLUSTER.

$env:SETUP_ALL = "true"

neon run --vault-password-file=$env:SECRETS_PASS "$env:SECRETS_GLOBAL" "$env:SECRETS_LOCAL" "$env:VARS_GLOBAL" "$env:VARS_LOCAL" -- `
	powershell -f setup-cluster.ps1 "$env:CLUSTER_SETUP_PATH\clusters\$env:CLUSTER\cluster.json"

if (-not $?)
{
	exit 1
}

#------------------------------------------------------------------------------
# Provision core databases and services.

if (-not $skipCoreServices)
{
	./setup-core-services.ps1 $clusterName

	if (-not $?)
	{
		exit 1
	}
}
