#Requires -Version 7.0 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

# Builds the NATS images and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# USAGE: pwsh -file ./publish.ps1 [-all]

param 
(
	[switch]$allVersions = $false,
    [switch]$nopush = $false
)

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NF_ROOT\\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

function Build
{
	param
	(
		[parameter(Mandatory=$true, Position=1)][string] $version,
		[switch]$latest = $false
	)
Log-DebugLine "*** PUBLISH-0:"

	$registry    = GetLibraryRegistry "nats"
	$tag         = $version
	$tagAsLatest = TagAsLatest

Log-DebugLine "*** PUBLISH-1:"
	# Build and publish the images.

	. ./build.ps1 -registry $registry -version $version -tag $tag
Log-DebugLine "*** PUBLISH-2:"
    PushImage "${registry}:$tag"
Log-DebugLine "*** PUBLISH-3:"

	if ($latest -and $tagAsLatest)
	{
Log-DebugLine "*** PUBLISH-4:"
		docker tag "${registry}:$tag" "${registry}:latest"
Log-DebugLine "*** PUBLISH-5:"
		ThrowOnExitCode
Log-DebugLine "*** PUBLISH-6:"

		PushImage "${registry}:latest"
Log-DebugLine "*** PUBLISH-7:"
	}
Log-DebugLine "*** PUBLISH-8:"
}

$noImagePush = $nopush

if ($allVersions)
{
}

Build "1.4.1" -latest
