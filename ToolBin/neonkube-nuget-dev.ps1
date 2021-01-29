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

$ErrorActionPreference = "Stop"

# Import the global project include file.

. $env:NF_ROOT/Powershell/includes.ps1

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the neonCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_NUGET_DEVFEED))
{
    "ERROR: This script is intended for maintainers only"
    ""
    "NC_NUGET_DEVFEED environment variable is not defined."
    "Maintainers should re-run the neonCLOUD [buildenv.cmd] script."

    return 1
}

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

# Sets the package version in the specified project file and makes a backup
# of the original project file named [$project.bak].

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project,
        [Parameter(Position=1, Mandatory=1)]
        [string]$version
    )

    "* SetVersion: ${project}:${version}"

    $projectPath    = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")
    $orgProjectFile = Get-Content "$projectPath" -Encoding utf8
    $regex          = [regex]'<Version>(.*)</Version>'
    $match          = $regex.Match($orgProjectFile)
    $orgVersion     = $match.Groups[1].Value
    $tmpProjectFile = $orgProjectFile.Replace("<Version>$orgVersion</Version>", "<Version>$version</Version>")

    Copy-Item "$projectPath" "$projectPath.bak"
    
    $tmpProjectFile | Out-File -FilePath "$projectPath" -Encoding utf8
}

# Restores the original project version for a project.

function RestoreVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

    "* Restore: ${project}:${version}"

    $projectPath = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")

    Copy-Item "$projectPath.bak" "$projectPath"
    Remove-Item "$projectPath.bak"
}

# Builds and publishes the project.

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

    $projectPath = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")

    dotnet pack $projectPath  -c Debug --include-symbols --include-source -o "$env:NF_BUILD\nuget"
    nuget push -Source $env:NC_NUGET_DEVFEED "$env:NF_BUILD\nuget\$project.$version.nupkg"
   
    # NOTE: We're not doing this because including source and symbols above because
    # doesn't seem to to work.
    #
	# dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NUGET_LOCAL_FEED"
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

$versionerKeyBase64 = [Convert]::ToBase64String(([System.Text.Encoding]::UTF8.GetBytes($env:NC_NUGET_VERSIONER_APIKEY)))

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
SetVersion Neon.Docker              $libraryVersion
SetVersion Neon.HyperV              $libraryVersion
SetVersion Neon.Service             $libraryVersion
SetVersion Neon.ModelGen            $libraryVersion
SetVersion Neon.Nats                $libraryVersion
SetVersion Neon.Postgres            $libraryVersion
SetVersion Neon.SSH                 $libraryVersion
SetVersion Neon.SSH.NET             $libraryVersion
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
SetVersion Neon.Kube.Wsl2           $kubeVersion
SetVersion Neon.Kube.XenServer      $kubeVersion
SetVersion Neon.Kube.Xunit          $kubeVersion

# Build and publish the projects.

Publish Neon.Cadence                $libraryVersion
Publish Neon.Cassandra              $libraryVersion
Publish Neon.Common                 $libraryVersion
Publish Neon.Couchbase              $libraryVersion
Publish Neon.Cryptography           $libraryVersion
Publish Neon.Docker                 $libraryVersion
Publish Neon.HyperV                 $libraryVersion
Publish Neon.Service                $libraryVersion
Publish Neon.ModelGen               $libraryVersion
Publish Neon.Nats                   $libraryVersion
Publish Neon.Postgres               $libraryVersion
Publish Neon.SSH                    $libraryVersion
Publish Neon.SSH.NET                $libraryVersion
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
Publish Neon.Kube.Wsl2              $kubeVersion
Publish Neon.Kube.XenServer         $kubeVersion
Publish Neon.Kube.Xunit             $kubeVersion

# Restore the project versions

RestoreVersion Neon.Cadence             
RestoreVersion Neon.Cassandra           
RestoreVersion Neon.Common              
RestoreVersion Neon.Couchbase           
RestoreVersion Neon.Cryptography        
RestoreVersion Neon.Docker              
RestoreVersion Neon.HyperV              
RestoreVersion Neon.Service             
RestoreVersion Neon.ModelGen            
RestoreVersion Neon.Nats                
RestoreVersion Neon.Postgres            
RestoreVersion Neon.SSH                 
RestoreVersion Neon.SSH.NET             
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
RestoreVersion Neon.Kube.Wsl2      
RestoreVersion Neon.Kube.XenServer      
RestoreVersion Neon.Kube.Xunit          

""
"** Package publication completed"
""
pause
