#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonHIVE [neon-proxy] image.
#
# Usage: powershell -file build.ps1 REGISTRY HAPROXY_VERSION DOTNET_VERSION TAG
#
# NOTE: The full .NET Core version must be specified (e.g. "2.1.5" instead of "2.1");

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True, Position=2)][string] $haProxyVersion,
	[parameter(Mandatory=$True, Position=3)][string] $dotnetVersion,
	[parameter(Mandatory=$True,Position=4)][string] $tag
)

"   "
"======================================="
"* NEON-PROXY:" + $tag
"======================================="

$appname = "neon-proxy"
$branch  = GitBranch

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build and publish the app to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_services_path\\$appname\\$appname.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] application and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "BRANCH=$branch" --build-arg "HAPROXY_VERSION=$haProxyVersion" --build-arg "DOTNET_VERSION=$dotnetVersion" --build-arg "APPNAME=$appname" . }

# Clean up

Exec { rm -r bin }
DeleteFolder _common
