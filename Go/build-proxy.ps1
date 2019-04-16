#------------------------------------------------------------------------------
# FILE:         build-proxy.ps1
# CONTRIBUTOR:  Jeff Lill
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
# them to $NF_BUILD.  No command line arguments are required.

$buildPath   = "$env:NF_BUILD"
$goPath      = "$env:NF_ROOT/Go"
$projectPath = "$goPath/src/github.com/loopieio/go-cadence-proxy"
$logPath     = "$buildPath/build-proxy.log"

# Create the NF_BUILD folder if it doesn't already exist.

if (!(test-path $buildPath))
{
    New-Item -ItemType Directory -Force -Path $buildPath
}

$orgDirectory = Get-Location

cd "$projectPath"
make -f Makefile >> "$logPath"
$exitCode = $lastExitCode

Set-Location $orgDirectory

if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: Cadence Proxy build failed.  Check build logs: $logPath"
    exit $exitCode
}
