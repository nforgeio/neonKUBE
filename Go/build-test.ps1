#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-test.ps1
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
# This script builds the [tests] GOLANG executables and writes
# them to: $NK_BUILD/go-test.
#
# USAGE: pwsh -file build-test.ps1

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

$env:GO111MODULE = "on"
$projectPath     = "$env:NK_ROOT\Go\test"
$buildPath       = "$env:NK_BUILD\go-test"
$logPath         = "$buildPath\build.log"

Push-Location $projectPath | Out-Null

try 
{
    # Ensure that the build output folder exists.

    [System.IO.Directory]::CreateDirectory($buildPath) | Out-Null

    #==============================================================================
    # BUILD CADENCE TESTS

    # Ensure that the build output folder exist.

    $outputPath = [System.IO.Path]::Combine($buildPath, "cadence")

    [System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

    # Common Cadence client configuration

    Set-Cwd "$projectPath\cadence"
    Copy-Item config.yaml "$outputPath\config.yaml"

    #----------------------------------------------------------
    # cwf-args

    Set-Cwd "$projectPath\cadence\cwf-args" | Out-Null

    Write-Output "Building cwf-args" > "$logPath"

    $env:GOOS   = "windows"
    $env:GOARCH = "amd64"

    go build -o "$outputPath\cwf-args.exe" . >> "$logPath" 2>&1
    ThrowOnExitCode

    Write-Output "Build success" >> "$logPath" 2>&1

    #==============================================================================
    # BUILD TEMPORAL TESTS

    # $todo(jefflill): Implement this!

    # Ensure that the build output folder exist.

    $outputPath = [System.IO.Path]::Combine($buildPath, "temporal")

    [System.IO.Directory]::CreateDirectory($outputPath) | Out-Null

    # Common Cadence client configuration

    Set-Cwd "$projectPath\temporal" | Out-Null
    Copy-Item config.yaml "$outputPath\config.yaml"

    #----------------------------------------------------------
    # cwf-args

    Set-Cwd "$projectPath\temporal\twf-args" | Out-Null

    Write-Output "Building twf-args" > "$logPath"

    $env:GOOS   = "windows"
    $env:GOARCH = "amd64"

    go build -o "$outputPath\twf-args.exe" . >> "$logPath" 2>&1
    ThrowOnExitCode

    Write-Output "Build success" >> "$logPath" 2>&1
}
catch
{
    Write-Exception $_
    exit 1
}
finally
{
    Pop-Location | Out-Null
}
