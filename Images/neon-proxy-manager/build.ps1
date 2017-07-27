#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds the neonCLUSTER [neon-proxy-manager] image.
#
# Usage: powershell -file build.ps1 VERSION [-latest]

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $version,    # like: "1.0.0"
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-PROXY-MANAGER " + $version
"======================================="

# Build and publish the [neon-proxy-manager] to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_services_path\\neon-proxy-manager\\neon-proxy-manager.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers neon-proxy-manager "$pwd\bin" }

# Build the images.

$registry = "neoncluster/neon-proxy-manager"

Exec { docker build -t "${registry}:$version"  --build-arg "APPNAME=neon-proxy-manager". }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

Exec { rm -r bin }
