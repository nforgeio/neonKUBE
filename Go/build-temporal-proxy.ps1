#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-temporal-proxy.ps1
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
# This script builds the [temporal-proxy] GOLANG executables and writes
# them to: $NK_BUILD.
#
# USAGE: pwsh -file build-temporal-proxy.ps1

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Initialize

$env:GOPATH   = "$env:NK_ROOT\Go"
$buildPath    = "$env:NK_BUILD"
$projectPath  = "$env:GOPATH\src\temporal-proxy"
$logPath      = "$buildPath\build-temporal-proxy.log"

Push-Cwd "$projectpath\cmd\temporalproxy" | Out-Null

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
    Write-Output "*                            WINDOWS TEMPORAL-PROXY                           *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS	= "windows"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\temporal-proxy.win.exe cmd\temporalproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             LINUX TEMPORAL-PROXY                            *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "linux"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\temporal-proxy.linux cmd\temporalproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             OS/X TEMPORAL-PROXY                             *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "darwin"
    $env:GOARCH = "amd64"

    $result = Invoke-CaptureStreams "go build -mod=vendor -ldflags=`"-w -s`" -v -o $buildPath\temporal-proxy.osx cmd\temporalproxy\main.go"
    Write-Output $result.alltext

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                      COMPRESSING TEMPORAL-PROXY BINARIES                    *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    # Compress the binaries to the [Neon.Temporal] project where they'll
    # be embedded as binary resources.

    $neonTemporalResourceFolder = "$env:NK_ROOT\Lib\Neon.Temporal\Resources"

    neon-build gzip "$buildPath\temporal-proxy.linux"   "$neonTemporalResourceFolder\temporal-proxy.linux.gz"   >> "$logPath" 2>&1
    ThrowOnExitCode

    neon-build gzip "$buildPath\temporal-proxy.osx"     "$neonTemporalResourceFolder\temporal-proxy.osx.gz"     >> "$logPath" 2>&1
    ThrowOnExitCode

    neon-build gzip "$buildPath\temporal-proxy.win.exe" "$neonTemporalResourceFolder\temporal-proxy.win.exe.gz" >> "$logPath" 2>&1
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
