#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonHIVE Ubuntu image with the specified .NET Core packages.
#
# Usage: powershell -file build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $version,
	[parameter(Mandatory=$True,Position=3)][string] $tag
)

"   "
"======================================="
"* UBUNTU-16.04-ASPNET " + $tag
"======================================="

$organization = DockerOrg
$branch       = GitBranch

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "VERSION=$version" . }

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

DeleteFolder _common
