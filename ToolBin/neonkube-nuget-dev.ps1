#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neonkube-nuget-dev.ps1
# CONTRIBUTOR:  Jeff Lill
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

# NOTE: This is script works only for maintainers with proper credentials.

# Publishes DEBUG builds of the NeonForge Nuget packages to the repo
# at https://nuget-dev.neoncloud.io so intermediate builds can be shared 
# by maintainers.
#
# USAGE: pwsh -f neonkube-nuget-dev.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -local          - Publishes to C:\nc-nuget-local
#       -localversion   - Use the local version number
#
# Generally, you'll use this script without any options to publish to the private
# feed in the neonCLOUD headend using the atomic counter there to update VERSION
# numbers, especially for shared branches and especially the master branch.
#
# During development on private branches, you may wish to use a local feed
# instead, which is simply the C:\nc-nuget-local folder.  This will be much 
# faster and will reduce the accumulation of packages in our private feed.
# Use the [-local] option for this.
#
# EMERGENCY USE:
# 
# By default, [-local] will still use the atomic versioner service in the
# headend to increment counters so that these versions monotonically increase
# across all packages published by all developers.  In an emergency such as
# when the headend services are down or when you're trying to work offline
# or on a poor connection, you can combine [-local-version] with [-local].
#
# This indicates that version numbers will be obtained from local counter files
# within the local feed folder:
#
#   C:\nc-nuget-local\neonKUBE.version.txt
#   C:\nc-nuget-local\neonLIBRARY.version.txt
#
# These simply hold the next version as an integer on the first line for 
# each set of packages.  You'll need to manually initialize these files
# with reasonable version numbers greater than any previously published
# packages.
#
# Once the emergency is over, you must to manually update the versions
# on the headend to be greater than any version published locally by any
# developer on the team and then republish all packages using the new
# version.

# $todo(jefflill):
#
# We should update the versioner to manage the entire version, not just
# incrementing the PATCH part of the version.  This would make it easier
# to recover from emergency use of the [-local-version] switch by simply
# needing to increment the MINOR component of the versions without needing
# to coordinate with developers to determine the maximum version published.
#
#   https://app.zenhub.com/workspaces/neonforge-6042ead6ec0efa0012c5facf/issues/nforgeio/neoncloud/173

param 
(
    [switch]$local        = $false,
    [switch]$localVersion = $false
)

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the neonCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_ROOT))
{
    "*** ERROR: This script is intended for maintainers only:"
    "           [NC_ROOT] environment variable is not defined."
    ""
    "           Maintainers should re-run the neonCLOUD [buildenv.cmd] script."

    return 1
}

# This needs to run with elevated privileges.

Request-AdminPermissions

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

    $projectPath    = [io.path]::combine($env:NK_ROOT, "Lib", "$project", "$project" + ".csproj")
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

    $projectPath = [io.path]::combine($env:NK_ROOT, "Lib", "$project", "$project" + ".csproj")

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

    $localIndicator = ""

    if ($local)
    {
        $localIndicator = " (local)"
    }

    ""
    "==============================================================================="
    "* Publishing: ${project}:${version}${localIndicator}"
    "==============================================================================="

    $projectPath = [io.path]::combine($env:NK_ROOT, "Lib", "$project", "$project" + ".csproj")

    dotnet pack $projectPath -c $config -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o "$env:NK_BUILD\nuget"
    ThrowOnExitCode

    if ($local)
    {
        nuget add -Source $env:NC_NUGET_LOCAL "$env:NK_BUILD\nuget\$project.$version.nupkg"
        ThrowOnExitCode
    }
    else
    {
        nuget push -Source $env:NC_NUGET_DEVFEED -ApiKey $devFeedApiKey "$env:NK_BUILD\nuget\$project.$version.nupkg" -SkipDuplicate -Timeout 600
        ThrowOnExitCode
    }
}

$msbuild     = $env:MSBUILDPATH
$nkRoot      = "$env:NK_ROOT"
$nkSolution  = "$nkRoot\neonKUBE.sln"
$branch      = GitBranch $nkRoot

if ($localVersion)
{
    $local = $true
}

if ($localVersion)
{
    # EMERGENCY MODE: Use local counters.

    $nkVersionPath = [System.IO.Path]::Combine($env:NC_NUGET_LOCAL, "neonKUBE.version.txt")
    $nlVersionPath = [System.IO.Path]::Combine($env:NC_NUGET_LOCAL, "neonLIBRARY.version.txt")

    if (![System.IO.File]::Exists("$nkVersionPath") -or ![System.IO.File]::Exists("$nlVersionPath"))
    {
        Write-Error "You'll need to manually initialize the local version files at:" -ErrorAction continue
        Write-Error ""                   -ErrorAction continue
        Write-Error "    $nkVersionPath" -ErrorAction continue
        Write-Error "    $nlVersionPath" -ErrorAction continue
        Write-Error "" -ErrorAction continue
        Write-Error "Create these files with the minor version number currently referenced" -ErrorAction continue
        Write-Error "by your local neonCLOUD solution:" -ErrorAction continue
        Write-Error "" -ErrorAction continue
        Write-Error "The easiest way to do this is to open the [neonCLOUD/Tools/neon-cli/neon-cli.csproj]" -ErrorAction continue
        Write-Error "file extract the minor version for the package references as described below:" -ErrorAction continue
        Write-Error "" -ErrorAction continue
        Write-Error "    neonKUBE.version.txt:    from Neon.Kube" -ErrorAction continue
        Write-Error "    neonLIBRARY.version.txt: from Neon.Common" -ErrorAction continue
        Write-Error "" -ErrorAction continue
        Write-Error "NOTE: These two version numbers are currently the same (Jan 2022), but they" -ErrorAction continue
        Write-Error "      may diverge at any time and will definitely diverge after we separate " -ErrorAction continue
        Write-Error "      neonLIBRARY and neonKUBE." -ErrorAction continue
        exit 1
    }

    $version = [int](Get-Content -TotalCount 1 $nlVersionPath).Trim()
    $version++
    [System.IO.File]::WriteAllText($nlVersionPath, $version)
    $libraryVersion = "10000.0.$version-dev-$branch"

    $version = [int](Get-Content -TotalCount 1 $nkVersionPath).Trim()
    $version++
    [System.IO.File]::WriteAllText($nkVersionPath, $version)
    $kubeVersion = "10000.0.$version-dev-$branch"
}
else
{
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

    # Retrieve any necessary credentials.

    $versionerKey  = Get-SecretValue "NUGET_VERSIONER_KEY" "group-devops"
    $devFeedApiKey = Get-SecretValue "NUGET_DEVFEED_KEY"   "group-devops"

    # Get the nuget versioner API key from the environment and convert it into a base-64 string.

    $versionerKeyBase64 = [Convert]::ToBase64String(([System.Text.Encoding]::UTF8.GetBytes($versionerKey)))

    # Submit PUTs request to the versioner service, specifying the counter name.  The service will
    # atomically increment the counter and return the next value.

    $reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonLIBRARY-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
    $libraryVersion = "10000.0.$reply-dev-$branch"

    $reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonKUBE-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
    $kubeVersion    = "10000.0.$reply-dev-$branch"
}

# We need to do a solution build to ensure that any tools or other dependencies 
# are built before we build and publish the individual packages.

Write-Info ""
Write-Info "********************************************************************************"
Write-Info "***                            CLEAN SOLUTION                                ***"
Write-Info "********************************************************************************"
Write-Info ""

& "$msbuild" "$nkSolution" $buildConfig -t:Clean -m -verbosity:quiet

if (-not $?)
{
    throw "ERROR: CLEAN FAILED"
}

Write-Info ""
Write-Info "********************************************************************************"
Write-Info "***                           RESTORE PACKAGES                               ***"
Write-Info "********************************************************************************"
Write-Info ""

& "$msbuild" "$nkSolution" -t:restore -verbosity:quiet

Write-Info  ""
Write-Info  "*******************************************************************************"
Write-Info  "***                           BUILD SOLUTION                                ***"
Write-Info  "*******************************************************************************"
Write-Info  ""

& "$msbuild" "$nkSolution" -p:Configuration=$config -restore -m -verbosity:quiet

if (-not $?)
{
    throw "ERROR: BUILD FAILED"
}

# We need to set the version first in all of the project files so that
# implicit package dependencies will work for external projects importing
# these packages.

SetVersion Neon.Cadence                     $libraryVersion
SetVersion Neon.Cassandra                   $libraryVersion
SetVersion Neon.Common                      $libraryVersion
SetVersion Neon.Couchbase                   $libraryVersion
SetVersion Neon.Cryptography                $libraryVersion
SetVersion Neon.CSharp                      $libraryVersion
SetVersion Neon.Deployment                  $libraryVersion
SetVersion Neon.Docker                      $libraryVersion
SetVersion Neon.JsonConverters              $libraryVersion
SetVersion Neon.HyperV                      $libraryVersion
SetVersion Neon.Service                     $libraryVersion
SetVersion Neon.ModelGen                    $libraryVersion
SetVersion Neon.ModelGenerator              $libraryVersion
SetVersion Neon.Nats                        $libraryVersion
SetVersion Neon.Postgres                    $libraryVersion
SetVersion Neon.SSH                         $libraryVersion
SetVersion Neon.Tailwind                    $libraryVersion
SetVersion Neon.Temporal                    $libraryVersion
SetVersion Neon.Web                         $libraryVersion
SetVersion Neon.WinTTY                      $libraryVersion
SetVersion Neon.WSL                         $libraryVersion
SetVersion Neon.XenServer                   $libraryVersion
SetVersion Neon.Xunit                       $libraryVersion
SetVersion Neon.Xunit.Cadence               $libraryVersion
SetVersion Neon.Xunit.Couchbase             $libraryVersion
SetVersion Neon.Xunit.Temporal              $libraryVersion
SetVersion Neon.Xunit.YugaByte              $libraryVersion
SetVersion Neon.YugaByte                    $libraryVersion

SetVersion Neon.Kube                        $kubeVersion
SetVersion Neon.Kube.Aws                    $kubeVersion
SetVersion Neon.Kube.Azure                  $kubeVersion
SetVersion Neon.Kube.BareMetal              $kubeVersion
SetVersion Neon.Kube.DesktopServer          $kubeVersion
SetVersion Neon.Kube.Google                 $kubeVersion
SetVersion Neon.Kube.GrpcProto              $kubeVersion
SetVersion Neon.Kube.Hosting                $kubeVersion
SetVersion Neon.Kube.HyperV                 $kubeVersion
SetVersion Neon.Kube.Models                 $kubeVersion
SetVersion Neon.Kube.Operator               $kubeVersion
SetVersion Neon.Kube.ResourceDefinitions    $kubeVersion
SetVersion Neon.Kube.Resources              $kubeVersion
SetVersion Neon.Kube.Setup                  $kubeVersion
SetVersion Neon.Kube.XenServer              $kubeVersion
SetVersion Neon.Kube.Xunit                  $kubeVersion

# Build and publish the projects.

Publish Neon.Cadence                        $libraryVersion
Publish Neon.Cassandra                      $libraryVersion
Publish Neon.Common                         $libraryVersion
Publish Neon.Couchbase                      $libraryVersion
Publish Neon.Cryptography                   $libraryVersion
Publish Neon.CSharp                         $libraryVersion
Publish Neon.Deployment                     $libraryVersion
Publish Neon.Docker                         $libraryVersion
Publish Neon.JsonConverters                 $libraryVersion
Publish Neon.HyperV                         $libraryVersion
Publish Neon.Service                        $libraryVersion
Publish Neon.ModelGen                       $libraryVersion
Publish Neon.ModelGenerator                 $libraryVersion
Publish Neon.Nats                           $libraryVersion
Publish Neon.Postgres                       $libraryVersion
Publish Neon.SSH                            $libraryVersion
Publish Neon.Tailwind                       $libraryVersion
Publish Neon.Temporal                       $libraryVersion
Publish Neon.Web                            $libraryVersion
Publish Neon.WinTTY                         $libraryVersion
Publish Neon.WSL                            $libraryVersion
Publish Neon.XenServer                      $libraryVersion
Publish Neon.Xunit                          $libraryVersion
Publish Neon.Xunit.Cadence                  $libraryVersion
Publish Neon.Xunit.Couchbase                $libraryVersion
Publish Neon.Xunit.Temporal                 $libraryVersion
Publish Neon.Xunit.YugaByte                 $libraryVersion
Publish Neon.YugaByte                       $libraryVersion

Publish Neon.Kube                           $kubeVersion
Publish Neon.Kube.Aws                       $kubeVersion
Publish Neon.Kube.Azure                     $kubeVersion
Publish Neon.Kube.BareMetal                 $kubeVersion
Publish Neon.Kube.DesktopServer             $kubeVersion
Publish Neon.Kube.Google                    $kubeVersion
Publish Neon.Kube.GrpcProto                 $kubeVersion
Publish Neon.Kube.Hosting                   $kubeVersion
Publish Neon.Kube.HyperV                    $kubeVersion
Publish Neon.Kube.Models                    $kubeVersion
Publish Neon.Kube.Operator                  $kubeVersion
Publish Neon.Kube.ResourceDefinitions       $kubeVersion
Publish Neon.Kube.Resources                 $kubeVersion
Publish Neon.Kube.Setup                     $kubeVersion
Publish Neon.Kube.XenServer                 $kubeVersion
Publish Neon.Kube.Xunit                     $kubeVersion

# Restore the project versions

RestoreVersion Neon.Cadence
RestoreVersion Neon.Cassandra
RestoreVersion Neon.Common
RestoreVersion Neon.Couchbase
RestoreVersion Neon.Cryptography
RestoreVersion Neon.CSharp
RestoreVersion Neon.Deployment
RestoreVersion Neon.Docker
RestoreVersion Neon.JsonConverters
RestoreVersion Neon.HyperV
RestoreVersion Neon.Service
RestoreVersion Neon.ModelGen
RestoreVersion Neon.ModelGenerator
RestoreVersion Neon.Nats
RestoreVersion Neon.Postgres
RestoreVersion Neon.SSH
RestoreVersion Neon.Tailwind
RestoreVersion Neon.Temporal
RestoreVersion Neon.Web
RestoreVersion Neon.WinTTY
RestoreVersion Neon.WSL
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
RestoreVersion Neon.Kube.DesktopServer
RestoreVersion Neon.Kube.Google
RestoreVersion Neon.Kube.GrpcProto
RestoreVersion Neon.Kube.Hosting
RestoreVersion Neon.Kube.HyperV
RestoreVersion Neon.Kube.Models
RestoreVersion Neon.Kube.Operator
RestoreVersion Neon.Kube.ResourceDefinitions
RestoreVersion Neon.Kube.Resources
RestoreVersion Neon.Kube.Setup
RestoreVersion Neon.Kube.XenServer
RestoreVersion Neon.Kube.Xunit

# Remove all of the generated nuget files so these don't accumulate.

Remove-Item "$env:NK_BUILD\nuget\*"

""
"** Package publication completed"
""
