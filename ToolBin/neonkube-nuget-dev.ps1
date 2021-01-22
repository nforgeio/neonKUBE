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

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the neonCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_NUGET_VERSIONER))
{
    "ERROR: This script is intended for maintainers only"
    ""
    "NC_NUGET_VERSIONER environment variable is not defined."
    "Maintainers should re-run the neonCLOUD [buildenv.cmd] script."

    return 1
}

if (!(Test-Path env:NC_NUGET_VERSIONER_APIKEY))
{
    "ERROR: This script is intended for maintainers only"
    ""
    "NC_NUGET_VERSIONER_APIKEY environment variable is not defined."
    "Maintainers should re-run the neonCLOUD [buildenv.cmd] script."

    return 1
}

# This needs to run with elevated privileges.

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    # Relaunch as an elevated process:
    Start-Process powershell.exe "-file",('"{0}"' -f $MyInvocation.MyCommand.Path) -Verb RunAs
    exit
}

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project,
        [Parameter(Position=1, Mandatory=1)]
        [string]$version
    )

""
"==============================================================================="
"* Publishing: ${project}:${version}"
"==============================================================================="

    $projectPath    = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")
    $orgProjectFile = Get-Content "$projectPath" -Encoding utf8
    $regex          = [regex]'<Version>(.*)</Version>'
    $match          = $regex.Match($orgProjectFile)
    $orgVersion     = $match.Groups[1].Value
    $tmpProjectFile = $orgProjectFile.Replace("<Version>$orgVersion</Version>", "<Version>$version</Version>")

    Copy-Item "$projectPath" "$projectPath.org"
    
    $tmpProjectFile | Out-File -FilePath "$projectPath" -Encoding utf8

    dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug -o "$env:NF_BUILD\nuget"
    nuget push -Source $env:NC_NUGET_DEVFEED "$env:NF_BUILD\nuget\$project.$version.nupkg"
   
    # NOTE: We're not doing this because including source and symbols above because
    # doesn't seem to to work.
    #
	# dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NUGET_LOCAL_FEED"

    # Restore the project file.

    Copy-Item "$projectPath.org" "$projectPath"
    Remove-Item "$projectPath.org"
}

# We're going to call the neonCLOUD nuget versioner service to atomically increment the 
# dev package version counters for the solution and then generate the full version for
# the packages we'll be publishing.  We'll use separate counters for the neonLIBRARY
# and neonKUBE packages.

# Get the nuget versioner API key from the environment and convert it into a base-64 string.

$versionerKeyBase64 = [Convert]::ToBase64String(([System.Text.Encoding]::UTF8.GetBytes($env:NC_NUGET_VERSIONER_APIKEY)))

# Submit PUTs request to the versioner service, specifying the counter name.  The service will
# atomically increment the counters and return the next value.

$reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonLIBRARY-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
$libraryVersion = "10000.0.$reply-dev"

$reply          = Invoke-WebRequest -Uri "$env:NC_NUGET_VERSIONER/counter/neonKUBE-dev" -Method 'PUT' -Headers @{ 'Authorization' = "Bearer $versionerKeyBase64" } 
$kubeVersion    = "10000.0.$reply-dev"

# Build and publish the projects.

Publish Neon.Cadence            $libraryVersion
Publish Neon.Cassandra          $libraryVersion
Publish Neon.Common             $libraryVersion
Publish Neon.Couchbase          $libraryVersion
Publish Neon.Cryptography       $libraryVersion
Publish Neon.Docker             $libraryVersion
Publish Neon.HyperV             $libraryVersion
Publish Neon.Service            $libraryVersion
Publish Neon.ModelGen           $libraryVersion
Publish Neon.Nats               $libraryVersion
Publish Neon.Postgres           $libraryVersion
Publish Neon.SSH                $libraryVersion
Publish Neon.SSH.NET            $libraryVersion
Publish Neon.Temporal           $libraryVersion
Publish Neon.Web                $libraryVersion
Publish Neon.XenServer          $libraryVersion
Publish Neon.Xunit              $libraryVersion
Publish Neon.Xunit.Cadence      $libraryVersion
Publish Neon.Xunit.Couchbase    $libraryVersion
Publish Neon.Xunit.Temporal     $libraryVersion
Publish Neon.Xunit.YugaByte     $libraryVersion
Publish Neon.YugaByte           $libraryVersion

Publish Neon.Kube               $kubeVersion
Publish Neon.Kube.Aws           $kubeVersion
Publish Neon.Kube.Azure         $kubeVersion
Publish Neon.Kube.BareMetal     $kubeVersion
Publish Neon.Kube.Google        $kubeVersion
Publish Neon.Kube.Hosting       $kubeVersion
Publish Neon.Kube.HyperV        $kubeVersion
Publish Neon.Kube.HyperVLocal   $kubeVersion
Publish Neon.Kube.XenServer     $kubeVersion
Publish Neon.Kube.Xunit         $kubeVersion

pause
