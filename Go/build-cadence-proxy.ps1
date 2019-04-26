#------------------------------------------------------------------------------
# FILE:         build-cadence-proxy.ps1
# CONTRIBUTOR:  John C Burnes
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
# This script builds the Cadence Proxy GOLANG executables and writes
# them to $NF_BUILD.
#
# USAGE: powershell -file build-cadence.ps1 [CONFIGURATION]
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

$env:GOPATH   = "$env:NF_ROOT\Go"
$buildPath    = "$env:NF_BUILD"
$projectPath  = "$env:GOPATH\src\github.com\loopieio\cadence-proxy"
$logPath      = "$buildPath\build-cadence-proxy.log"
$orgDirectory = Get-Location

Set-Location "$projectpath\cmd\cadenceproxy"

if (!(test-path $buildPath))
{
    New-Item -ItemType Directory -Force -Path $buildPath
}

# Change to project path
Set-Location $projectPath

# Build the WINDOWS binary
$env:GOOS	= "windows"
$env:GOARCH = "amd64"
go build -i -ldflags="-w -s" -v -o $buildPath\cadence-proxy.win.exe cmd\cadenceproxy\main.go

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: Cadence Proxy WINDOWS build failed.  Check build logs: $logPath"
    Set-Location $orgDirectory
    exit $exitCode
}

# Build the LINUX binary
$env:GOOS   = "linux"
$env:GOARCH = "amd64"
go build -i -ldflags="-w -s" -v -o $buildPath\cadence-proxy.linux cmd\cadenceproxy\main.go

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: Cadence Proxy  LINUX build failed.  Check build logs: $logPath"
    Set-Location $orgDirectory
    exit $exitCode
}

# Build the OSX binary
$env:GOOS   = "darwin"
$env:GOARCH = "amd64"
go build -i -ldflags="-w -s" -v -o $buildPath\cadence-proxy.osx cmd\cadenceproxy\main.go

$exitCode = $lastExitCode

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: Cadence Proxy OSX build failed.  Check build logs: $logPath"
    Set-Location $orgDirectory
    exit $exitCode
}

# Compress the binaries for RELEASE builds only to make DEBUG builds faster.

# $hack(jeff.lill):
#
# Note that for DEBUG builds, we're just going to copy the files without
# compressing them.  This is a bit confusing because the file extension
# will still be ".gz", but the [Neon.Cadence] assembly is the only consumer
# for these files and it's smart enough to read the header to distingush
# between compressed and uncompressed files.

if ($buildConfig -eq "Release")
{
    neon-build gzip $buildPath\cadence-proxy.linux $buildPath\cadence-proxy.linux.gz
    neon-build gzip $buildPath\cadence-proxy.osx $buildPath\cadence-proxy.osx.gz
    neon-build gzip $buildPath\cadence-proxy.win.exe $buildPath\cadence-proxy.win.exe.gz
}
else
{
    neon-build copy $buildPath\cadence-proxy.linux $buildPath\cadence-proxy.linux.gz
    neon-build copy $buildPath\cadence-proxy.osx $buildPath\cadence-proxy.osx.gz
    neon-build copy $buildPath\cadence-proxy.win.exe $buildPath\cadence-proxy.win.exe.gz
}

# Go back to the original directory
Set-Location $orgDirectory
