#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
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

$appname  = "neon"
$registry = "neoncluster/neon-cli"

# Build and publish the app to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_tools_path\\neon-cli\\neon-cli.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Invoke the tool to retrieve its version number.  Note that we need
# to use [--direct] because we haven't published the tool's image
# to the registry yet.

$version=$(& dotnet "$pwd\bin\$appname.dll" --direct version -n)

# Build the image.

Exec { docker build -t "${registry}:$version" --build-arg "APPNAME=$appname" . }

if ($latest)
{
	Exec { docker tag "${registry}:$version" "${registry}:latest"}
}

PushImage "${registry}:$version"

if ($latest)
{
	PushImage "${registry}:latest"
}

# Clean up

Exec { Remove-Item -Force .rnd }
Exec { DeleteFile .rnd }
