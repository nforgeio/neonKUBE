#------------------------------------------------------------------------------
# Setup core cluster databases and services (non-docker).
#
# usage: powershell -file setup-base-services.ps1 HIVE-NAME
#
# ARGUMENTS:
#
# ARGUMENTS:
#
#   HIVE-NAME		- Identifies the target hive definition in the [hives]
#	    			  subfolder.  Note that this DOES NOT include the [.json]
#					  file extension).  Example: "home-small"

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $hiveName
)

# Initialize the environment.

cd "$env:NF_ROOT\Devops\test"
./env.ps1 $hiveName

# Configure the infrastructure services (databases on pets, etc).

.\setup-registry.ps1 $hiveName
