#------------------------------------------------------------------------------
# Setup core cluster databases and services (non-docker).
#
# usage: powershell -file setup-base-services.ps1 CLUSTER-NAME
#
# ARGUMENTS:
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
./env.ps1 $clusterName

# Configure the infrastructure services (databases on pets, etc).

.\setup-registry.ps1 $clusterName
