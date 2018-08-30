#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonHIVE Varnish base images.

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $varnishVersion,    # Specific Varish version (like "6.0.0")
	[parameter(Mandatory=$True,Position=3)][string] $tag
)

"   "
"======================================="
"* NEON-VARNISH:" + $tag
"======================================="

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "FAMILY=$varnishFamily" --build-arg "BRANCH=$branch" --build-arg "VERSION=$varnishVersion" . }

# Clean up

DeleteFolder _common
