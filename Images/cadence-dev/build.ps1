#------------------------------------------------------------------------------
# FILE:         build.ps1
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

# Builds the cadence-dev Cadence Server test images.
#
# USAGE: pwsh -file build.ps1 REGISTRY VERSION GO-VERSION TAG 

param 
(
	[parameter(Mandatory=$true,Position=1)][string] $registry,
	[parameter(Mandatory=$true,Position=2)][string] $version,       # Cadence server version
	[parameter(Mandatory=$true,Position=3)][string] $goVersion,     # Go version
	[parameter(Mandatory=$true,Position=4)][string] $uiVersion,     # Cadence ui version
	[parameter(Mandatory=$true,Position=5)][string] $tag
)

"   "
"==========================================================="
"* CADENCE-DEV:" + $tag
"* GO_VERSION:" + $goVersion
"* CADENCE_VERSION:" + $version
"* CADENCE_UI_VERSION:" + $uiVersion
"==========================================================="

# Build the image.

Exec { docker build -t "${registry}:$tag" --build-arg "VERSION=$version" --build-arg "GO_VERSION=$goVersion" --build-arg "UI_VERSION=$uiVersion" . }
