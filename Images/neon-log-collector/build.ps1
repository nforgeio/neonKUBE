#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE LLC.  All rights reserved.
#
# Builds the base [neon-log-collector] image.
#
# Usage: powershell -file build.ps1 REGISTRY TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True,Position=3)][string] $tag

)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

"   "
"======================================="
"* neon-log-collector:" + $tag
"======================================="

$appname      = "neon-cluster-manager"
$organization = DockerOrg

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.
$maxmind_key = neon run -- cat "_...$src_images_path\neon-log-collector\maxmind"
Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "CLUSTER_VERSION=$neonKUBE_Version" --build-arg "APPNAME=$appname" --build-arg "MAXMIND_KEY=$maxmind_key" . }

# Clean up

DeleteFolder _common