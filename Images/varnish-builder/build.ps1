#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonHIVE Varnish base images.

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=4)][string] $tag
)

"   "
"======================================="
"* VARNISH-BUILDER:" + $tag
"======================================="

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" . }

# Clean up

DeleteFolder _common
