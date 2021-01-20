 #------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  John C Burns
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

# Builds the Cadence Server test images including Cassandra 
# and pushes them to Docker Hub.
#
# NOTE: You must be logged into Docker Hub.
#
# Usage: powershell -file ./publish.ps1 [-all]

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
		[parameter(Mandatory=$true, Position=2)][string] $goVersion,
		[parameter(Mandatory=$true, Position=3)][string] $uiVersion,
		[switch]$latest = $false
	)

	$registry    = GetLibraryRegistry "cadence-dev"
	$tag         = $version
	$tagAsLatest = TagAsLatest

	# Build and publish the images.

	. ./build.ps1 -registry $registry -version $version -goVersion $goVersion -uiVersion $uiVersion -tag $tag
    PushImage "${registry}:$tag"

	if ($latest -and $tagAsLatest)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:latest" }
		PushImage "${registry}:latest"
	}
}

$noImagePush = $nopush

if ($allVersions)
{
    Build v0.5.9 -goVersion 1.12.6 -uiVersion 3.3.1 -latest
}

Build v0.11.0 -goVersion 1.13.6 -uiVersion v3.12.0 -latest