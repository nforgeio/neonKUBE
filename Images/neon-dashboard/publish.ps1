#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Builds the neon-dashboard images and pushes them to Docker Hub.
#
# NOTE: You must be already logged into the target container registry.
#
# USAGE: pwsh -f publish.ps1 [-all]

param 
(
	[switch]$allVersions = $false,
    [switch]$nopush = $false
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NK_ROOT\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[parameter(Mandatory=$true, Position=1)][string] $version,
		[switch]$latest = $false
	)

	$registry    = GetKubeSetupRegistry "neon-dashboard"
	$tag         = "$version"
	$tagAsLatest = TagAsLatest
	$tagOverride = $env:DEBUG_TAG

	if (![string]::IsNullOrEmpty($tagOverride))
	{
		$tag    = $tagOverride
		$latest = $false
	}

	Log-ImageBuild $registry $tag

	# Build and publish the images.

	. ./build.ps1 -registry $registry -tag $tag
    Push-DockerImage "${registry}:${tag}"

	if ($latest -and $tagAsLatest)
	{
		$result = Invoke-CaptureStreams "docker tag ${registry}:${tag} ${registry}:latest" -interleave
		Push-DockerImage ${registry}:latest
	}
}

$noImagePush = $nopush

if ($allVersions)
{
}

Build $neonKUBE_Tag -latest
