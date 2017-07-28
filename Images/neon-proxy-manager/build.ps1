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

$appname  = "neon-proxy-manager"
$registry = "neoncluster/$appname"

# Build and publish the app to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_services_path\\$appname\\$appname.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Build the images.

Exec { docker build -t "${registry}:$version" --build-arg "APPNAME=$appname" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

# Clean up

Exec { rm -r bin }
