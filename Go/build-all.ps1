#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-all.ps1
# CONTRIBUTOR:  John C Burns
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
#
# This script builds all GOLANG projects.
#
# USAGE: pwsh -file build-all.ps1
#
# ARGUMENTS: NONE

$env:NK_GOROOT   = "$env:NK_ROOT\Go"
$env:GO111MODULE = "on"

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

Push-Cwd $env:NK_GOROOT | Out-Null

try
{
    # Perform the builds

    $result = Invoke-CaptureStreams "pwsh -file ./build-cadence-proxy.ps1"
    $result = Invoke-CaptureStreams "pwsh -file ./build-temporal-proxy.ps1"
    $result = Invoke-CaptureStreams "pwsh ./build-test.ps1"
}
catch
{
    Write-Exception $_
    exit 1
}
finally
{
    Pop-Cwd | Out-Null
}
