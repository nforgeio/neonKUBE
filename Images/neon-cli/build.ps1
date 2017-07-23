#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.
#
# Builds the neonCLUSTER [neon-cli] image.
#
# Usage: powershell -file build.ps1 [-latest]
#
# Note that the build script retrieves the version number from the tool itself.

param 
(
	[switch]$latest = $False
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* NEON-CLI "
"======================================="

# Build and publish the [neon-cli] binaries to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_tools_path\\neon\\neon.csproj" -c Release -o "$pwd\bin" }

# Invoke the tool to retrieve its version number.  Note that we need
# to use [--direct] because we haven't published the tool's image
# to the registry yet.

$version=$(& dotnet "$pwd\bin\neon.dll" --direct version -n)

# Build the image.

$registry = "neoncluster/neon-cli"

Exec { docker build -t "${registry}:$version" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

Exec { rm -r bin }

PushImage "${registry}:$version"

if ($latest)
{
	PushImage "${registry}:latest"
}

