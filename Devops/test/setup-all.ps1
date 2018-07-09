#------------------------------------------------------------------------------
# Configures the TEST workload neonHIVE hive from scratch including provisioning
# the XenServer virtual machines and intializing the pets, databases, and services.
#
# usage: powershell -file setup-all.ps1 hiveName [imageTag] [OPTIONS]
#
# ARGUMENTS:
#
#   hiveName		    - Identifies the target hive definition in the [hives]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "wrt-00-prod"
#
#	imageTag   	    	- Optionally specifies the Docker image tag to use instead
#						  of [latest] when deploying images to the hive.
#
# OPTIONS:
#
#	-debugsetup			- Run hive prepare and setup in DEBUG mode.
#
#	-skipPrepare		- Skip the hive prepare step.
#
#	-skipSetup			- Skip the hive setup step.
#
#	-skipCoreServices	- Skip deploying core services (like databases).
#						  This implies setting [-skipServices].
#
#	-skipServices		- Skip deploying hive services.

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $hiveName,
	[parameter(Mandatory=$False, Position=2)]  [string] $imageTag = "",
    [switch] $noshim           = $False,
	[switch] $debugsetup       = $False,
    [switch] $skipPrepare      = $False,
    [switch] $skipSetup        = $False,
    [switch] $skipCoreServices = $False,
	[switch] $skipServices     = $False
)
	
# Initialize the environment.

cd "$env:NF_ROOT\Devops\test"
./env.ps1 $hiveName -nologin

# Convert the optional parameters into environment variables.

if ($debugsetup)
{
    $env:SETUP_DEBUG = "--debug"
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
# Provision and setup the base neonHIVE.

$env:SETUP_ALL = "true"

neon run --vault-password-file=$env:SECRETS_PASS "$env:SECRETS_GLOBAL" "$env:SECRETS_LOCAL" "$env:VARS_GLOBAL" "$env:VARS_LOCAL" -- `
	powershell -f setup-hive.ps1 "$env:CLUSTER_SETUP_PATH\hives\$env:HIVE\hive.json"

if (-not $?)
{
	exit 1
}

#------------------------------------------------------------------------------
# Provision core databases and services.

if (-not $skipCoreServices)
{
	./setup-core-services.ps1 $hiveName

	if (-not $?)
	{
		exit 1
	}
}
