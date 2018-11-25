#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonHIVE Varnish base images.

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=2)][string] $varnishFamily,     # Varnish version family (like "60")
	[parameter(Mandatory=$True,Position=3)][string] $varnishVersion,    # Specific Varish version (like "6.0.0")
	[parameter(Mandatory=$True,Position=4)][string] $tag
)

"   "
"======================================="
"* VARNISH:" + $tag
"======================================="

$organization = DockerOrg
$branch       = GitBranch

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" --build-arg "FAMILY=$varnishFamily" --build-arg "VERSION=$varnishVersion" . }

# Clean up

DeleteFolder _common
