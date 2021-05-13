#Requires -Version 7.0 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build.ps1
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

# Builds the NATS image.
#
# USAGE: pwsh -file build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$true,Position=1)][string] $registry,
	[parameter(Mandatory=$true,Position=2)][string] $version,
	[parameter(Mandatory=$true,Position=3)][string] $tag
)

Log-ImageBuild $registry $tag

docker pull nats:$version-linux
ThrowOnExitCode

[System.IO.File]::AppendAllText("C:\Temp\log.txt", "BUILD: ****************************`r`n")
$dir = Get-Location
[System.IO.File]::AppendAllText("C:\Temp\log.txt", "*** 0: dir: $dir`r`n")

docker build -t ${registry}:$tag --build-arg VERSION=$version .

[System.IO.File]::AppendAllText("C:\Temp\log.txt", "*** 1: exit-code: $lastExitCode`r`n")

ThrowOnExitCode

[System.IO.File]::AppendAllText("C:\Temp\log.txt", "*** 2: `r`n")
[System.IO.File]::AppendAllText("C:\Temp\log.txt", "BUILD: ****************************`r`n")
