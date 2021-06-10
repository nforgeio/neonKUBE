#Requires -Version 7.0 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neonkube-nuget-dev.ps1
# CONTRIBUTOR:  Jeff Lill
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

# Publishes DEBUG builds of the NeonForge Nuget packages to the repo
# at http://nuget-dev.neoncloud.io so intermediate builds can be shared 
# by maintainers.
#
# NOTE: This is script works only for maintainers with proper credentials.

# Import the global solution include file.

. $env:NF_ROOT/Powershell/includes.ps1

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the neonCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_USER))
{
    "*** ERROR: This script is intended for maintainers only:"
    "           [NC_USER] environment variable is not defined."
    ""
    "           Maintainers should re-run the neonCLOUD [buildenv.cmd] script."

    return 1
}

# This needs to run with elevated privileges.

Request-AdminPermissions

# Retrieve any necessary credentials.

$versionerKey  = Get-SecretValue "NUGET_VERSIONER_KEY" "group-devops"
$devFeedApiKey = Get-SecretValue "NUGET_DEVFEED_KEY" "group-devops"

# We're going to build the Debug configuration so debugging will be easier.

$config = "Debug"

#------------------------------------------------------------------------------
# Sets the package version in the specified project file and makes a backup
# of the original project file named [$project.bak].

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$version
    )

    "* SetVersion: ${project}:${version}"

    $projectPath    = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")
    $orgProjectFile = Get-Content "$projectPath" -Encoding utf8
    $regex          = [regex]'<Version>(.*)</Version>'
    $match          = $regex.Match($orgProjectFile)
    $orgVersion     = $match.Groups[1].Value
    $tmpProjectFile = $orgProjectFile.Replace("<Version>$orgVersion</Version>", "<Version>$version</Version>")

    if (!(Test-Path "$projectPath.bak"))
    {
        Copy-Item "$projectPath" "$projectPath.bak"
    }
    
    $tmpProjectFile | Out-File -FilePath "$projectPath" -Encoding utf8
}

#------------------------------------------------------------------------------
# Restores the original project version for a project.

function RestoreVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project
    )

    "* Restore: ${project}"

    $projectPath = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")

    Copy-Item "$projectPath.bak" "$projectPath"
    Remove-Item "$projectPath.bak"
}

#------------------------------------------------------------------------------
# Builds and publishes the project packages.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$version
    )

    ""
    "==============================================================================="
    "* Publishing: ${project}:${version}"
    "==============================================================================="

    $projectPath = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")

    dotnet pack $projectPath -c $config -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o "$env:NF_BUILD\nuget"
    ThrowOnExitCode

    nuget push -Source $env:NC_NUGET_DEVFEED -ApiKey $devFeedApiKey "$env:NF_BUILD\nuget\$project.$version.nupkg"
    ThrowOnExitCode
}

# We need to do a  solution build to ensure that any tools or other dependencies 
# are built before we build and publish the individual packages.

Write-Info  ""
Write-Info  "*******************************************************************************"
Write-Info  "***                            BUILD SOLUTION                               ***"
Write-Info  "*******************************************************************************"
Write-Info  ""

$msbuild     = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$nfRoot      = "$env:NF_ROOT"
$nfSolution  = "$nfRoot\neonKUBE.sln"

& "$msbuild" "$nfSolution" -p:Configuration=$config -restore -m -verbosity:quiet

if (-not $?)
{
    throw "ERROR: BUILD FAILED"
}

# We're going to call the neonCLOUD nuget versioner service to atomically increment the 
# dev package version counters for the solution and then generate the full version for
# the packages we'll be publishing.  We'll use separate counters for the neonLIBRARY
# and neonKUBE packages.
#
# The package versions will also include the current branch appended to the preview tag
# so a typical package version will look like:
#
#       10000.0.VERSION-dev-master
#
# where we use major version 10000 as a value that will never be exceeded by a real
# release, VERSION is automatically incremented for every package published, [master]
# in this case is the current branch at the time of publishing and [-dev] indicates
# that this is a non-production release.
#
# NOTE: We could have used a separate counter for each published branch but we felt it
# would this would be easier to manage by having all recent packages published from all
# branches have versions near each other.

$branch = GitBranch $env:NF_ROOT

# Get the nuget versioner API key from the environment and convert it into a base-64 string.

$versionerKeyBase64 = [Convert]::ToBase64String(([System.Text.Encoding]::UTF8.GetBytes($versionerKey)))

# Submit PUTs request to the versioner service, specifying the counter name.  The service will
# atomically increment the counter and return the next value.

$reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonLIBRARY-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
$libraryVersion = "10000.0.$reply-dev-$branch"

$reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonKUBE-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
$kubeVersion    = "10000.0.$reply-dev-$branch"

# We need to set the version first in all of the project files so that
# implicit package dependencies will work for external projects importing
# these packages.

SetVersion Neon.Cadence             $libraryVersion
SetVersion Neon.Cassandra           $libraryVersion
SetVersion Neon.Common              $libraryVersion
SetVersion Neon.Couchbase           $libraryVersion
SetVersion Neon.Cryptography        $libraryVersion
SetVersion Neon.Deployment          $libraryVersion
SetVersion Neon.Docker              $libraryVersion
SetVersion Neon.HyperV              $libraryVersion
SetVersion Neon.Service             $libraryVersion
SetVersion Neon.ModelGen            $libraryVersion
SetVersion Neon.ModelGenerator      $libraryVersion
SetVersion Neon.Nats                $libraryVersion
SetVersion Neon.Postgres            $libraryVersion
SetVersion Neon.SSH                 $libraryVersion
SetVersion Neon.Temporal            $libraryVersion
SetVersion Neon.Web                 $libraryVersion
SetVersion Neon.XenServer           $libraryVersion
SetVersion Neon.Xunit               $libraryVersion
SetVersion Neon.Xunit.Cadence       $libraryVersion
SetVersion Neon.Xunit.Couchbase     $libraryVersion
SetVersion Neon.Xunit.Temporal      $libraryVersion
SetVersion Neon.Xunit.YugaByte      $libraryVersion
SetVersion Neon.YugaByte            $libraryVersion

SetVersion Neon.Kube                $kubeVersion
SetVersion Neon.Kube.Aws            $kubeVersion
SetVersion Neon.Kube.Azure          $kubeVersion
SetVersion Neon.Kube.BareMetal      $kubeVersion
SetVersion Neon.Kube.Google         $kubeVersion
SetVersion Neon.Kube.Hosting        $kubeVersion
SetVersion Neon.Kube.HyperV         $kubeVersion
SetVersion Neon.Kube.HyperVLocal    $kubeVersion
SetVersion Neon.Kube.Setup          $kubeVersion
SetVersion Neon.Kube.Services       $kubeVersion
SetVersion Neon.Kube.XenServer      $kubeVersion
SetVersion Neon.Kube.Xunit          $kubeVersion

# Build and publish the projects.

Publish Neon.Cadence                $libraryVersion
Publish Neon.Cassandra              $libraryVersion
Publish Neon.Common                 $libraryVersion
Publish Neon.Couchbase              $libraryVersion
Publish Neon.Cryptography           $libraryVersion
Publish Neon.Deployment             $libraryVersion
Publish Neon.Docker                 $libraryVersion
Publish Neon.HyperV                 $libraryVersion
Publish Neon.Service                $libraryVersion
Publish Neon.ModelGen               $libraryVersion
Publish Neon.ModelGenerator         $libraryVersion
Publish Neon.Nats                   $libraryVersion
Publish Neon.Postgres               $libraryVersion
Publish Neon.SSH                    $libraryVersion
Publish Neon.Temporal               $libraryVersion
Publish Neon.Web                    $libraryVersion
Publish Neon.XenServer              $libraryVersion
Publish Neon.Xunit                  $libraryVersion
Publish Neon.Xunit.Cadence          $libraryVersion
Publish Neon.Xunit.Couchbase        $libraryVersion
Publish Neon.Xunit.Temporal         $libraryVersion
Publish Neon.Xunit.YugaByte         $libraryVersion
Publish Neon.YugaByte               $libraryVersion

Publish Neon.Kube                   $kubeVersion
Publish Neon.Kube.Aws               $kubeVersion
Publish Neon.Kube.Azure             $kubeVersion
Publish Neon.Kube.BareMetal         $kubeVersion
Publish Neon.Kube.Google            $kubeVersion
Publish Neon.Kube.Hosting           $kubeVersion
Publish Neon.Kube.HyperV            $kubeVersion
Publish Neon.Kube.HyperVLocal       $kubeVersion
Publish Neon.Kube.Setup             $kubeVersion
Publish Neon.Kube.Services          $kubeVersion
Publish Neon.Kube.XenServer         $kubeVersion
Publish Neon.Kube.Xunit             $kubeVersion

# Restore the project versions

RestoreVersion Neon.Cadence
RestoreVersion Neon.Cassandra
RestoreVersion Neon.Common
RestoreVersion Neon.Couchbase
RestoreVersion Neon.Cryptography
RestoreVersion Neon.Deployment
RestoreVersion Neon.Docker
RestoreVersion Neon.HyperV
RestoreVersion Neon.Service
RestoreVersion Neon.ModelGen
RestoreVersion Neon.ModelGenerator
RestoreVersion Neon.Nats
RestoreVersion Neon.Postgres
RestoreVersion Neon.SSH
RestoreVersion Neon.Temporal
RestoreVersion Neon.Web
RestoreVersion Neon.XenServer
RestoreVersion Neon.Xunit
RestoreVersion Neon.Xunit.Cadence
RestoreVersion Neon.Xunit.Couchbase
RestoreVersion Neon.Xunit.Temporal
RestoreVersion Neon.Xunit.YugaByte
RestoreVersion Neon.YugaByte

RestoreVersion Neon.Kube
RestoreVersion Neon.Kube.Aws
RestoreVersion Neon.Kube.Azure
RestoreVersion Neon.Kube.BareMetal
RestoreVersion Neon.Kube.Google
RestoreVersion Neon.Kube.Hosting
RestoreVersion Neon.Kube.HyperV
RestoreVersion Neon.Kube.HyperVLocal
RestoreVersion Neon.Kube.Setup
RestoreVersion Neon.Kube.Services
RestoreVersion Neon.Kube.XenServer
RestoreVersion Neon.Kube.Xunit

""
"** Package publication completed"
""
