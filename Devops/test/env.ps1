#------------------------------------------------------------------------------
# Configures the environment for a script to execute against a specific hive.
# This script is intended to be called by other scripts rather than directly
# by the system operator.
#
# usage: powershell -file env.ps1 HIVE-NAME [-nologin]
#
# ARGUMENTS:
#
#   HIVE-NAME			- Identifies the target hive definition in the [hives]
#	    				  subfolder.  Note that this DOES NOT include the [.json]
#						  file extension).  Example: "home-small"
#
#	-nologin			- Pass this if the script is not supposed to log into
#						  the hive (defaults to FALSE).

param 
(
    [parameter(Mandatory=$True,Position=1)][string] $hiveName,
    [parameter(Mandatory=$False,Position=2)][switch] $nologin = $False
)

# NOTE: This script configures the following environment variables:
#	
#	HIVE_NODE_TEMPLATE_USERNAME	- SSH username for the neonHIVE node template (like: [sysadmin])
#	HIVE_NODE_TEMPLATE_PASSWORD	- SSH password for the neonHIVE node template (like: [sysadmin0000])
#   HIVE					    - neonHIVE name
#	HIVE_LOG_FOLDER			- path to the setup log folder
#	HIVE_MAX_PARALLEL		- maximum setup steps to perform in parallel (like: 10)
#	HIVE_LOGIN				- neonHIVE login name (like: root@home-small)

# Initialize environment variables.

$env:HIVE               = $hiveName
$env:HIVE_LOGIN         = "root@$env:HIVE"
$env:CLUSTER_SETUP_PATH = "$env:NF_ROOT\Devops\test"
$env:HIVE_MAX_PARALLEL  = 10

# Hive secrets are persisted to the Ansible compatible variable files
# called [secrets.yaml].  This file is encrypted using the [neon-git]
# Ansible password.

$env:SECRETS_PASS   = "neon-git"
$env:SECRETS_GLOBAL = "$env:CLUSTER_SETUP_PATH\secrets.yaml"
$env:SECRETS_LOCAL  = "$env:CLUSTER_SETUP_PATH\hives\$env:HIVE\secrets.yaml"
$env:VARS_GLOBAL    = "$env:CLUSTER_SETUP_PATH\secrets.yaml"
$env:VARS_LOCAL     = "$env:CLUSTER_SETUP_PATH\hives\$env:HIVE\secrets.yaml"

# Hive secret YAML files need to have Linux-style line endings, so we're
# going to convert these here.

unix-text --recursive $env:CLUSTER_SETUP_PATH\*.yml
unix-text --recursive $env:CLUSTER_SETUP_PATH\*.yaml

# Ensure that the setup log folder exists and is cleared.

$env:HIVE_LOG_FOLDER = "C:\hive-logs\$env:HIVE"

if (Test-Path $env:HIVE_LOG_FOLDER)
{
	del "$env:HIVE_LOG_FOLDER\*.log"
}
else
{
	mkdir "$env:HIVE_LOG_FOLDER"
}

if (-not $nologin)
{
	neon login $env:HIVE_LOGIN

	if (-not $?)
	{
		exit 1
	}
}