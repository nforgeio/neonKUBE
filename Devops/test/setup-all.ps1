#------------------------------------------------------------------------------
# Configures the TEST workload neonCLUSTER cluster from scratch including provisioning
# the XenServer virtual machines and intializing the pets, databases, and services.
#
# usage: powershell -file setup.ps1 CLUSTER-NAME
#
# ARGUMENTS:
#
#   CLUSTER-NAME		- Identifies the target cluster definition in the [clusters]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "home-small"

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $clusterName
)
	
# Initialize the environment.

cd "$env:NF_ROOT\Devops\test"
./env.ps1 $clusterName -nologin

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
