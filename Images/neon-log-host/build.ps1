#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE LLC.  All rights reserved.
#
# Builds the base [neon-log-host] image.
#
# Usage: powershell -file build.ps1 REGISTRY TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=3)][string] $tag

)

"   "
"======================================="
"* NEON-LOG-HOST:" + $tag
"======================================="

$organization = DockerOrg
$branch       = GitBranch

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" . }
