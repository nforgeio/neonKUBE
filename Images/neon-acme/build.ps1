#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Builds the Neon [neon-acme] image.
#
# USAGE: pwsh -file build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

$appname      = "neon-acme"
$organization = KubeSetupRegistryOrg

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

$result = Invoke-CaptureStreams "docker build -t ${registry}:${tag} --build-arg `"APPNAME=$appname`" ." -interleave

# Clean up

DeleteFolder _common
