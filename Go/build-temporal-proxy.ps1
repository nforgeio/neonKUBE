#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build-temporal-proxy.ps1
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
# This script builds the [temporal-proxy] GOLANG executables and writes
# them to: $NF_BUILD.
#
# USAGE: pwsh -file build-temporal-proxy.ps1

# Import the global solution include file.

. $env:NF_ROOT/Powershell/includes.ps1

$env:GOPATH   = "$env:NF_ROOT\Go"
$buildPath    = "$env:NF_BUILD"
$projectPath  = "$env:GOPATH\src\temporal-proxy"
$logPath      = "$buildPath\build-temporal-proxy.log"

Push-Cwd "$projectpath\cmd\temporalproxy" | Out-Null

try
{
    # Ensure that the build output folder exists and remove any existing log file.

    [System.IO.Directory]::CreateDirectory($buildPath);

    if ([System.IO.File]::Exists($logPath))
    {
        [System.IO.File]::Delete($logPath)
    }

    # Change to project path

    Set-Cwd $projectPath

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                            WINDOWS CADENCE-PROXY                            *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS	= "windows"
    $env:GOARCH = "amd64"

    go build -i -mod=vendor -ldflags="-w -s" -v -o $buildPath\temporal-proxy.win.exe cmd\temporalproxy\main.go >> "$logPath" 2>&1

    $exitCode = $lastExitCode

    if ($exitCode -ne 0)
    {
        throw "*** ERROR[exitcode=$exitCode]: [temporal-proxy] WINDOWS build failed.  Check build logs: $logPath"
    }

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             LINUX CADENCE-PROXY                             *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "linux"
    $env:GOARCH = "amd64"

    go build -i -mod=vendor -ldflags="-w -s" -v -o $buildPath\temporal-proxy.linux cmd\temporalproxy\main.go >> "$logPath" 2>&1

    $exitCode = $lastExitCode

    if ($exitCode -ne 0)
    {
        throw "*** ERROR[exitcode=$exitCode]: [temporal-proxy] LINUX build failed.  Check build logs: $logPath"
    }

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                             OS/X CADENCE-PROXY                              *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    $env:GOOS   = "darwin"
    $env:GOARCH = "amd64"

    go build -i -mod=vendor -ldflags="-w -s" -v -o $buildPath\temporal-proxy.osx cmd\temporalproxy\main.go >> "$logPath" 2>&1

    $exitCode = $lastExitCode

    if ($exitCode -ne 0)
    {
        throw "*** ERROR[exitcode=$exitCode]: [temporal-proxy] OSX build failed.  Check build logs: $logPath"
    }

    Write-Output " "                                                                               >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output "*                      COMPRESSING CADENCE-PROXY BINARIES                     *" >> $logPath 2>&1
    Write-Output "*******************************************************************************" >> $logPath 2>&1
    Write-Output " "                                                                               >> $logPath 2>&1

    # Compress the binaries to the [Neon.Temporal] project where they'll
    # be embedded as binary resources.

    $neonTemporalResourceFolder = "$env:NF_ROOT\Lib\Neon.Temporal\Resources"

    neon-build gzip "$buildPath\temporal-proxy.linux"   "$neonTemporalResourceFolder\temporal-proxy.linux.gz"   >> "$logPath" 2>&1
    neon-build gzip "$buildPath\temporal-proxy.osx"     "$neonTemporalResourceFolder\temporal-proxy.osx.gz"     >> "$logPath" 2>&1
    neon-build gzip "$buildPath\temporal-proxy.win.exe" "$neonTemporalResourceFolder\temporal-proxy.win.exe.gz" >> "$logPath" 2>&1
}
catch
{
    Write-Exception $_
}
finally
{
    Pop-Cwd | Out-Null
}
