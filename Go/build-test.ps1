#Requires -Version 7.0 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-test.ps1
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
# This script builds the [tests] GOLANG executables and writes
# them to: $NF_BUILD/go-test.
#
# USAGE: pwsh -file build-test.ps1

# Import the global solution include file.

. $env:NF_ROOT/Powershell/includes.ps1

$env:GO111MODULE = "on"
$projectPath     = "$env:NF_ROOT\Go\test"
$buildPath       = "$env:NF_BUILD\go-test"
$logPath         = "$buildPath\build.log"
$orgDirectory    = Get-Cwd

Set-Cwd $projectPath

# Ensure that the build output folder exist.

if (!(test-path $buildPath))
{
    New-Item -ItemType Directory -Force -Path $buildPath
}

#==============================================================================
# BUILD CADENCE TESTS

# Ensure that the build output folder exist.

$outputPath = Join-Path -Path $buildPath -ChildPath "cadence"

if (!(test-path $outputPath))
{
    New-Item -ItemType Directory -Force -Path $outputPath
}

# Common Cadence client configuration

Set-Cwd "$projectPath\cadence"
cp config.yaml "$outputPath\config.yaml"

#----------------------------------------------------------
# cwf-args

Set-Cwd "$projectPath\cadence\cwf-args"

echo "Building cwf-args" > "$logPath"

$env:GOOS   = "windows"
$env:GOARCH = "amd64"

go build -o "$outputPath\cwf-args.exe" . 6>&1 2>&1 >> "$logPath"

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: [go-test] WINDOWS build failed.  Check build logs: $logPath"
    Set-Cwd $orgDirectory
    exit $exitCode
}

echo "Build success" 6>&1 2>&1 >> "$logPath"

Set-Cwd $orgDirectory

#==============================================================================
# BUILD TEMPORAL TESTS

# $todo(jefflill): Implement this!

# Ensure that the build output folder exist.

$outputPath = Join-Path -Path $buildPath -ChildPath "temporal"

if (!(test-path $outputPath))
{
    New-Item -ItemType Directory -Force -Path $outputPath
}

# Common Cadence client configuration

Set-Cwd "$projectPath\temporal"
cp config.yaml "$outputPath\config.yaml"

#----------------------------------------------------------
# cwf-args

Set-Cwd "$projectPath\temporal\twf-args"

echo "Building twf-args" > "$logPath"

$env:GOOS   = "windows"
$env:GOARCH = "amd64"

go build -o "$outputPath\twf-args.exe" . 6>&1 2>&1 >> "$logPath"

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: [go-test] WINDOWS build failed.  Check build logs: $logPath"
    Set-Cwd $orgDirectory
    exit $exitCode
}

echo "Build success" 6>&1 2>&1 >> "$logPath"

Set-Cwd $orgDirectory
