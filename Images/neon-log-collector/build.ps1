#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the base [neon-log-collector] image.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,    # like: "2017.02.08"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-LOG-COLLECTOR " + $version
"======================================="

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

# Build the image.

$registry = "neoncluster/neon-log-collector"

Exec { docker build -t "${registry}:${version}" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

# Clean up

Exec { Remove-Item -Recurse _common }
Exec { DeleteFile .rnd }
