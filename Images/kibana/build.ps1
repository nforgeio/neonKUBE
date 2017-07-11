#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds a neonCLUSTER Kibana image with the specified version, subversion
# and majorversion.  The image built will be a slightly modified version of the 
# Kibana reference.
#
# Usage: powershell -file build.ps1 VERSION [SUBVERSION] [MAJORVERSION] [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,                # like: "5.0.0"
	[parameter(Mandatory=$False,Position=2)][string] $subversion = "-",      # like: "5.0"
	[parameter(Mandatory=$False,Position=3)][string] $majorversion = "-",    # like: "5"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* KIBANA v" + $version
"======================================="

# Build the images.

$registry           = "neoncluster/kibana";
$dockerTemplatePath = "Dockerfile.template";
$dockerFilePath     = "Dockerfile";

Exec { copy $dockerTemplatePath $dockerFilePath }
Exec { text replace-var "-VERSION=$version" $dockerFilePath }

Exec { docker build -f $dockerFilePath -t "${registry}:$version" . }

if ($subversion -ne "-")
{
	Exec { docker tag "${registry}:$version" "${registry}:$subversion"}
}

if ($majorversion -ne "-")
{
	Exec { docker tag "${registry}:$version" "${registry}:$majorversion"}
}

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

Exec { del $dockerFilePath }
