#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds a neonHIVE Node.js base image.
#
# Usage: powershell -file build.ps1 REGISTRY TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $tag
)

"   "
"======================================="
"* NODE:" + $tag
"======================================="

# Build the image.

Exec { docker build -t "${registry}:$tag" . }
