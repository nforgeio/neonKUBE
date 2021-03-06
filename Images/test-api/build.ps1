﻿#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

# Builds the test container image.
#
# Usage: powershell -file build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$true,Position=1)][string] $registry,
	[parameter(Mandatory=$true,Position=2)][string] $version,
	[parameter(Mandatory=$true,Position=3)][string] $tag
)


Log-ImageBuild $registry $tag

$appname           = "test-api"
$organization      = LibraryRegistryOrg
$base_organization = KubeBaseRegistryOrg

# Build and publish the app to a local [bin] folder.

DeleteFolder bin

Exec { mkdir bin }
Exec { dotnet publish "$nfServices\\$appname\\$appname.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] (application) and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "ORGANIZATION=$organization" --build-arg "BASE_ORGANIZATION=$base_organization" --build-arg "CLUSTER_VERSION=neonkube-$neonKUBE_Version" --build-arg "APPNAME=$appname" . }

# Clean up

DeleteFolder bin
