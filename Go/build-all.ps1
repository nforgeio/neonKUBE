#------------------------------------------------------------------------------
# FILE:         build-all.ps1
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
#
# This script builds all GOLANG projects.
#
# USAGE: pwsh -file build-all.ps1 [CONFIGURATION]
#
# ARGUMENTS:
#
#       -buildConfig Debug  - Optionally specifies the build configuration,
#                             either "Debug" or "Release".  This defaults
#                             to "Debug".

param 
(
    [parameter(Mandatory=$false)][string] $buildConfig = "Debug"
)

$env:NF_GOROOT = "$env:NF_ROOT\Go"

Push-Cwd $env:NF_GOROOT

Start-Process -FilePath powershell.exe -ArgumentList "./build-cadence-proxy.ps1", "-buildConfig $buildConfig" -Wait -NoNewWindow
Start-Process -FilePath powershell.exe -ArgumentList "./build-temporal-proxy.ps1", "-buildConfig $buildConfig" -Wait -NoNewWindow
Start-Process -FilePath powershell.exe -ArgumentList "./build-test.ps1" -Wait -NoNewWindow

Pop-Cwd
