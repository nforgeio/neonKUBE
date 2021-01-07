#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Builds the Neon [test-cadence] image.
#
# Usage: powershell -file build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

Log-ImageBuild $registry $tag

$appname      = "test-cadence"
$organization = RegistryOrg
$branch       = GitBranch

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build and publish the app to a local [bin] folder.

DeleteFolder bin

Exec { mkdir bin }
Exec { dotnet publish "$src_services_path\\$appname\\$appname.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "APPNAME=$appname" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" . }

# Clean up

DeleteFolder bin
DeleteFolder _common
