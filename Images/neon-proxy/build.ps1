#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonCLUSTER [neon-proxy] image.
#
# Usage: powershell -file build.ps1REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $registry,
	[parameter(Mandatory=$True, Position=2)][string] $version,
	[parameter(Mandatory=$True,Position=3)][string] $tag
)

"   "
"======================================="
"* NEON-PROXY:" + $tag
"======================================="

# Copy the common scripts.

DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Unzip the latest Consul binaries to a temporary [consul-binaries] folder so 
# they can be copied into the image.  The folder will be deleted further below.

DeleteFolder consul-binaries

mkdir consul-binaries
7z e -y "$image_root\\_artifacts\\consul_latest_linux_amd64.zip" -oconsul-binaries

# Unzip the latest Vault binaries to a temporary [vault-binaries] folder so 
# they can be copied into the image.  The folder will be deleted further below.

DeleteFolder vault-binaries

mkdir vault-binaries
7z e -y "$image_root\\_artifacts\\vault_current_linux_amd64.zip" -ovault-binaries

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$version" . }

# Clean up

sleep 5 # Docker sometimes appears to hold references to files we need
		# to delete so wait for a bit.

DeleteFolder _common
DeleteFolder vault-binaries
DeleteFolder consul-binaries
