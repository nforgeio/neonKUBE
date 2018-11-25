#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonHIVE Kibana image with the specified version.
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
"* KIBANA:" + $tag
"======================================="

$organization = DockerOrg
$branch       = GitBranch

# Build and publish the images.

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" --build-arg "VERSION=$version" . }

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

DeleteFolder _common
