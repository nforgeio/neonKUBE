#------------------------------------------------------------------------------
# FILE:         build-test.ps1
# CONTRIBUTOR:  John C Burns
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
#
# This script builds the [tests] GOLANG executables and writes
# them to: $NF_BUILD/go-test.
#
# USAGE: powershell -file build-test.ps1

$env:GO111MODULE = "on"
$projectPath     = "$env:NF_ROOT\Go\test"
$buildPath       = "$env:NF_BUILD\go-test"
$logPath         = "$buildPath\build.log"
$orgDirectory    = Get-Location

Set-Location $projectPath

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

Set-Location "$projectPath\cadence"
cp config.yaml "$outputPath\config.yaml"

#----------------------------------------------------------
# cwf-args

Set-Location "$projectPath\cadence\cwf-args"

echo "Building cwf-args" > "$logPath"

$env:GOOS   = "windows"
$env:GOARCH = "amd64"

go build -o "$outputPath\cwf-args.exe" . >> "$logPath" 2>&1

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: [go-test] WINDOWS build failed.  Check build logs: $logPath"
    Set-Location $orgDirectory
    exit $exitCode
}

echo "Build success" >> "$logPath" 2>&1

Set-Location $orgDirectory

#==============================================================================
# BUILD TEMPORAL TESTS

# $todo(jefflill): Implement this!




#-----------------------------------------------------------
# Misc repo config code: (archiving this just in case)
#
# set GO111MODULE=on
# go get go.uber.org/cadence
# go build -o C:\src\neonKUBE\Build\go-test\cadence\cwf-args.exe .
# cp config.yaml C:\src\neonKUBE\Build\go-test\cadence\config.yaml
