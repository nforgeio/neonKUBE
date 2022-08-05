#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-cadence-proxy.ps1
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
# This script builds the [cadence-proxy] GOLANG executables and writes
# them to: $NK_BUILD.
#
# USAGE: pwsh -file build-cadence-proxy.ps1

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Initialize

$env:GOPATH  = "$env:NK_ROOT\Go"
$buildPath   = "$env:NK_BUILD"
$projectPath = "$env:GOPATH\src\github.com\cadence-proxy"
$logPath     = "$buildPath\build-cadence-proxy.log"

Push-Cwd "$projectpath\cmd\cadenceproxy" | Out-Null

try
{
    # Ensure that the build output folder exists and remove any existing log file.

    [System.IO.Directory]::CreateDirectory($buildPath) | Out-Null

    if ([System.IO.File]::Exists($logPath))
    {
        [System.IO.File]::Delete($logPath)
    }

    # Change to project path

    Set-Cwd $projectPath | Out-Null

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                            WINDOWS CADENCE-PROXY                            *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS	= "windows"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\cadence-proxy.win.exe cmd\cadenceproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             LINUX CADENCE-PROXY                             *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "linux"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\cadence-proxy.linux cmd\cadenceproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             OS/X CADENCE-PROXY                              *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "darwin"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\cadence-proxy.osx cmd\cadenceproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                      COMPRESSING CADENCE-PROXY BINARIES                     *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    # Compress the binaries to the [Neon.Cadence] project where they'll
    # be embedded as binary resources.

    $neonCadenceResourceFolder = "$env:NK_ROOT\Lib\Neon.Cadence\Resources"

    neon-build gzip "$buildPath\cadence-proxy.linux"   "$neonCadenceResourceFolder\cadence-proxy.linux.gz"   >> "$logPath" 2>&1
    ThrowOnExitCode

    neon-build gzip "$buildPath\cadence-proxy.osx"     "$neonCadenceResourceFolder\cadence-proxy.osx.gz"     >> "$logPath" 2>&1
    ThrowOnExitCode

    neon-build gzip "$buildPath\cadence-proxy.win.exe" "$neonCadenceResourceFolder\cadence-proxy.win.exe.gz" >> "$logPath" 2>&1
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
