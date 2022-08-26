#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
#
# Builds the Neon [neon-acme] image.
#
# USAGE: pwsh -f build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

$appname      = "neon-acme"
$organization = SdkRegistryOrg

# Build and publish the app to a local [bin] folder.

DeleteFolder bin

mkdir bin
ThrowOnExitCode

dotnet publish "$nkServices\$appname\$appname.csproj" -c Release -o "$pwd\bin"
ThrowOnExitCode

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

core-layers $appname "$pwd\bin"
ThrowOnExitCode

# Build the image.

$result = Invoke-CaptureStreams "docker build -t ${registry}:${tag} --build-arg `"APPNAME=$appname`" --build-arg `"ORGANIZATION=$organization`" ." -interleave

# Clean up

DeleteFolder bin
DeleteFolder _common
