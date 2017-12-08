#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds the default [neon-proxy-vault] service image.
#
# Usage: powershell -file build.ps1

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-PROXY-VAULT"
"======================================="

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

# Build the images.

$registry = "neoncluster/neon-proxy-vault"

Exec { docker build -t "${registry}:latest" . }

# Clean up

Exec { Remove-Item -Recurse _common }
Exec { DeleteFile .rnd }
