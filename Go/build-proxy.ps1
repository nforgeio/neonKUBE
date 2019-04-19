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

$env:GOPATH   = "$env:NF_ROOT\Go"
$buildPath    = "$env:NF_BUILD"
$projectPath  = "$env:GOPATH\src\github.com\loopieio\cadence-proxy"
$logPath      = "$buildPath\build-proxy.log"
$orgDirectory = Get-Location


# Create the NF_BUILD folder if it doesn't already exist.

# install dependencies using dep (via dep ensure)
go get -u github.com/golang/dep/cmd/dep
cd "$projectpath\cmd\cadenceproxy"
& "$env:GOPATH\bin\dep.exe" ensure

if (!(test-path $buildPath))
{
    New-Item -ItemType Directory -Force -Path $buildPath
}

# change to project path
cd $projectPath

# build the windows binary
go build -i -ldflags="-w -s" -v -o $buildPath\gocadenceproxy.exe cmd\cadenceproxy\main.go

# build the linux binary
$env:GOOS    = "linux"
$env:GOARCH  = "amd64"
go build -i -ldflags="-w -s" -v -o $buildPath\gocadenceproxy cmd\cadenceproxy\main.go

# build the OSX binary
#$env:GOOS    = "darwin"
#go build -o -i -ldflags="-w -s" -v $buildPath\gocadenceproxy cmd\cadenceproxy\main.go

# set exit code
$exitCode = $lastExitCode

# cd back to the original directory
Set-Location $orgDirectory

# catch any build errors and exit with exit code
if ($exitCode -ne 0)
{
    Write-Error "*** ERROR: Cadence Proxy build failed.  Check build logs: $logPath"
    exit $exitCode
}
