#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonCLUSTER Ubuntu image with the specified .NET Core packages.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,                # .NET Core runtime version, like: "2.0.0"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* UBUNTU-16.04-DOTNET v" + $version
"======================================="

# Build the images.

# Copy the common scripts.

if (Test-Path _common)
{
	Exec { Remove-Item -Recurse _common }
}

Exec { mkdir _common }
Exec { copy ..\_common\*.* .\_common }

$registry = "neoncluster/ubuntu-16.04-dotnet"

Exec { docker build -t "${registry}:$version" --build-arg "VERSION=$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

Exec { Remove-Item -Recurse _common }
Exec { DeleteFile .rnd }
