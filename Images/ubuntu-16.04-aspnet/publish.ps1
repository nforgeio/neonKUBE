#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

# Builds all of the supported Ubuntu/.NET Core images and pushes them to Docker Hub.
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
		[parameter(Mandatory=$true, Position=1)][string] $dotnetVersion,
		[switch]$latest = $false
	)

	$registry = GetRegistry "ubuntu-16.04-aspnet"
	$date     = UtcDate
	$branch   = GitBranch
	$tag      = "$branch-$dotnetVersion"
	
	# Build and publish the images.

	. ./build.ps1 -registry $registry -tag $tag -version $dotnetVersion
	PushImage "${registry}:$tag"

	if (IsRelease)
	{
		Exec { docker tag "${registry}:$tag" "${registry}:$dotnetVersion" }
		PushImage "${registry}:$dotnetVersion"

		Exec { docker tag "${registry}:$tag" "${registry}:${dotnetVersion}-${date}" }
		PushImage "${registry}:$branch-$dotnetVersion"
	}

	if ($latest)
	{
		if (TagAsLatest)
		{
			Exec { docker tag "${registry}:$tag" "${registry}:latest" }
			PushImage "${registry}:latest"
		}

        Exec { docker tag "${registry}:$tag" "${registry}:${branch}-latest" }
		PushImage "${registry}:${branch}-latest"
	}
}

$noImagePush = $nopush

if ($allVersions)
{
}

Build 2.1 -latest
