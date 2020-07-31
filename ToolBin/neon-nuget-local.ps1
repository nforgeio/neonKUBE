#------------------------------------------------------------------------------
# FILE:         neon-nuget-local.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

# Publishes DEBUG builds of the NeonForge Nuget packages to the local
# file system at: %NF_BUILD%\nuget.

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    # Relaunch as an elevated process:
    Start-Process powershell.exe "-file",('"{0}"' -f $MyInvocation.MyCommand.Path) -Verb RunAs
    exit
}

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	neon-build pack-version "$env:NF_ROOT\neonLIBRARY-version.txt" "$env:NF_ROOT\Lib\$project\$project.csproj"
}

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NF_build\nuget"
}

# Copy the version from [$/product-version] into [$/Lib/Neon/Common/Build.cs]

& neon-build build-version

# Update the project versions.

SetVersion Neon.Cadence
SetVersion Neon.Common
SetVersion Neon.Couchbase
SetVersion Neon.Cryptography
SetVersion Neon.Docker
SetVersion Neon.HyperV
SetVersion Neon.Service
SetVersion Neon.ModelGen
SetVersion Neon.Nats
SetVersion Neon.SSH.NET
SetVersion Neon.Temporal
SetVersion Neon.Web
SetVersion Neon.XenServer
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Cadence
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.Kube
SetVersion Neon.Xunit.Temporal

# Build and publish the projects.

Publish Neon.Cadence
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Cryptography
Publish Neon.Docker
Publish Neon.HyperV
Publish Neon.Service
Publish Neon.ModelGen
Publish Neon.Nats
Publish Neon.SSH.NET
Publish Neon.Temporal
Publish Neon.Web
Publish Neon.XenServer
Publish Neon.Xunit
Publish Neon.Xunit.Cadence
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.Kube
Publish Neon.Xunit.Temporal

# Remove the generated standard nuget packages and replace them with the
# packages including symbols and source code.

# Get-ChildItem "$env:NF_BUILD\nuget\*.symbols.nupkg"  | Rename-Item -NewName { $_.Name -replace '.symbols.nupkg', '.symbols.tmp' }
# Remove-Item -Path "$env:NF_BUILD\nuget\*.nupkg"
# Get-ChildItem "$env:NF_BUILD\nuget\*.symbols.tmp"  | Rename-Item -NewName { $_.Name -replace '.symbols.tmp', '.nupkg' }

pause
