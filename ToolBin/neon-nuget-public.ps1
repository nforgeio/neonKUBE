#------------------------------------------------------------------------------
# FILE:         neon-nuget-public.ps1
# CONTRIBUTOR:  Jeff Lill
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

# Publishes RELEASE builds of the NeonForge Nuget packages to the
# local file system and public Nuget.org repositories.

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

    "$project"
	neon-build pack-version "$env:NF_ROOT\neonLIBRARY-version.txt" "$env:NF_ROOT\Lib\$project\$project.csproj"
}

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Release -o "$env:NF_BUILD\nuget"

	$libraryVersion = Get-Content "$env:NF_ROOT\neonLIBRARY-version.txt" -First 1

    if (Test-Path "$env:NF_ROOT\Lib\$project\prerelease.txt")
    {
        $prerelease = Get-Content "$env:NF_ROOT\Lib\$project\prerelease.txt" -First 1
        $prerelease = $prerelease.Trim()

        if ($prerelease -ne "")
        {
            $prerelease = "-" + $prerelease
        }
    }
    else
    {
        $prerelease = ""
    }

	nuget push -Source nuget.org "$env:NF_BUILD\nuget\$project.$libraryVersion$prerelease.nupkg"
}

# Copy the version from [$/product-version] into [$/Lib/Neon/Common/Build.cs]

neon-build build-version

# Update the project versions.

SetVersion Neon.Cadence
SetVersion Neon.Cassandra
SetVersion Neon.Common
SetVersion Neon.Couchbase
SetVersion Neon.Cryptography
SetVersion Neon.Docker
SetVersion Neon.HyperV
SetVersion Neon.Kube
SetVersion Neon.Kube.Aws
SetVersion Neon.Kube.Azure
SetVersion Neon.Kube.BareMetal
SetVersion Neon.Kube.Google
SetVersion Neon.Kube.Hosting
SetVersion Neon.Kube.HyperV
SetVersion Neon.Kube.HyperVLocal
SetVersion Neon.Kube.XenServer
SetVersion Neon.Service
SetVersion Neon.ModelGen
SetVersion Neon.Nats
SetVersion Neon.Postgres
SetVersion Neon.SSH
SetVersion Neon.SSH.NET
SetVersion Neon.Temporal
SetVersion Neon.Web
SetVersion Neon.XenServer
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Cadence
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.Kube
SetVersion Neon.Xunit.Temporal
SetVersion Neon.Xunit.YugaByte
SetVersion Neon.YugaByte

# Build and publish the projects.

Publish Neon.Cadence
Publish Neon.Cassandra
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Cryptography
Publish Neon.Docker
Publish Neon.HyperV
Publish Neon.Kube
Publish Neon.Kube.Aws
Publish Neon.Kube.Azure
Publish Neon.Kube.BareMetal
Publish Neon.Kube.Google
Publish Neon.Kube.Hosting
Publish Neon.Kube.HyperV
Publish Neon.Kube.HyperVLocal
Publish Neon.Kube.XenServer
Publish Neon.Service
Publish Neon.ModelGen
Publish Neon.Nats
Publish Neon.Postgres
Publish Neon.SSH
Publish Neon.SSH.NET
Publish Neon.Temporal
Publish Neon.Web
Publish Neon.XenServer
Publish Neon.Xunit
Publish Neon.Xunit.Cadence
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.Kube
Publish Neon.Xunit.Temporal
Publish Neon.Xunit.YugaByte
Publish Neon.YugaByte

pause
