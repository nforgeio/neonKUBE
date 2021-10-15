#Requires -Version 7.1.3 -RunAsAdministrator
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

# param 
# (
#     [parameter(Mandatory=$false)][string] $buildConfig = "Debug"
# )

$buildConfig = "Debug"

$env:NF_GOROOT   = "$env:NF_ROOT\Go"
$env:GO111MODULE = "on"

# Import the global solution include file.

. $env:NF_ROOT/Powershell/includes.ps1

Push-Cwd $env:NF_GOROOT | Out-Null

try
{
    # Perform the build

    # $hack(jefflill):
    #
    # We'e seeing intermittent GO build failures where GO complains about a bad command line
    # (or something) the first time a proxy binary is built for an OS/architecture but then
    # the same exact build command works the next time it's run.  Perhaps this is due to
    # vendoring weirdness but I don't really know.
    #
    # We're going to address this by running the build once again on failures.

    & pwsh -file ./build-cadence-proxy.ps1 -buildConfig $buildConfig
    
    if ($lastExitCode -ne 0)
    {
        & pwsh -file ./build-cadence-proxy.ps1 -buildConfig $buildConfig
        ThrowOnExitCode
    }

    & pwsh -file ./build-temporal-proxy.ps1 -buildConfig $buildConfig
    
    if ($lastExitCode -ne 0)
    {
        & pwsh -file ./build-temporal-proxy.ps1 -buildConfig $buildConfig
        ThrowOnExitCode
    }

    # The tests don't seem to have the intermittent build issue.

    & pwsh ./build-test.ps1
    ThrowOnExitCode
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
