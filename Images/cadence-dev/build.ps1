#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  John C Burns
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

# Builds the cluster cadence-dev images.
#
# Usage: powershell -file build.ps1 REGISTRY VERSION GO-VERSION TAG 

param 
(
	[parameter(Mandatory=$true,Position=1)][string] $registry,
	[parameter(Mandatory=$true,Position=2)][string] $version,       # Cadence version
	[parameter(Mandatory=$true,Position=3)][string] $goVersion,     # Go version
	[parameter(Mandatory=$true,Position=4)][string] $uiVersion,     # Cadence ui version
	[parameter(Mandatory=$true,Position=5)][string] $tag
)

"   "
"======================================="
"* CADENCE-DEV:" + $tag
"* GO_VERSION:" + $goVersion
"* CADENCE_VERSION:" + $version
"* CADENCE_UI_VERSION:" + $uiVersion
"======================================="

# Copy the common scripts.
DeleteFolder _common

mkdir _common
copy ..\_common\*.* .\_common

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$version" --build-arg "GO_VERSION=$goVersion" --build-arg "UI_VERSION=$uiVersion" . }

# Clean up
DeleteFolder _common
