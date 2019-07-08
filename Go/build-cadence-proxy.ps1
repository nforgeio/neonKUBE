#------------------------------------------------------------------------------
# FILE:         build-cadence-proxy.ps1
# CONTRIBUTOR:  John C Burns
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
# This script builds the [cadence-proxy] GOLANG executables and writes
# them to $NF_BUILD.
#
# USAGE: powershell -file build-cadence.ps1

$env:GOPATH   = "$env:NF_ROOT\Go"
$buildPath    = "$env:NF_BUILD"
$projectPath  = "$env:GOPATH\src\github.com\cadence-proxy"
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
    Write-Error "*** ERROR: [cadence-proxy] WINDOWS build failed.  Check build logs: $logPath"
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
    Write-Error "*** ERROR: [cadence-proxy] LINUX build failed.  Check build logs: $logPath"
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
    Write-Error "*** ERROR: [cadence-proxy] OSX build failed.  Check build logs: $logPath"
    Set-Location $orgDirectory
    exit $exitCode
}

# Compress the binaries to the [Neon.Cadence] project where they'll
# be embedded as binary resources.
$neonCadenceResourceFolder = "$env:NF_ROOT\Lib\Neon.Cadence\Resources"
neon-build gzip "$buildPath\cadence-proxy.linux"   "$neonCadenceResourceFolder\cadence-proxy.linux.gz"
neon-build gzip "$buildPath\cadence-proxy.osx"     "$neonCadenceResourceFolder\cadence-proxy.osx.gz"
neon-build gzip "$buildPath\cadence-proxy.win.exe" "$neonCadenceResourceFolder\cadence-proxy.win.exe.gz"

#---------------------------------------------------------------------
# $todo(jeff.lill): Remove this after we complete the Java-style port:

$neonCadenceResourceFolder = "$env:NF_ROOT\Lib\Neon.Cadence2\Resources"
neon-build gzip "$buildPath\cadence-proxy.linux"   "$neonCadenceResourceFolder\cadence-proxy.linux.gz"
neon-build gzip "$buildPath\cadence-proxy.osx"     "$neonCadenceResourceFolder\cadence-proxy.osx.gz"
neon-build gzip "$buildPath\cadence-proxy.win.exe" "$neonCadenceResourceFolder\cadence-proxy.win.exe.gz"
#---------------------------------------------------------------------

# Go back to the original directory
Set-Location $orgDirectory
