#------------------------------------------------------------------------------
# FILE:         neon-nuget-local.ps1
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

# Publishes DEBUG builds of the NeonForge Nuget packages to the local
# file system at: %NF_BUILD%\nuget.

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
        [int]$version
    )

    # The package will be published to [%NUGET_LOCAL_FEED%\NAME\VERSION\NAME.VERSION.nupkg] where
    # NAME is the package name and VERSION is the version.  We need to ensure that the directories
    # exist first and then build and copy the package there.  We'll need to inspect the project
    # file passed to discover the package name and version.

    $newVersion     = "10000.0.$version-local"
    $publishPath    = [io.path]::combine($env:NUGET_LOCAL_FEED, "$project.$newVersion")
    $projectPath    = [io.path]::combine($env:NF_ROOT, "Lib", "$project", "$project" + ".csproj")
    $orgProjectFile = Get-Content "$projectPath" -Encoding utf8
    $regex          = [regex]'<Version>(.*)</Version>'
    $match          = $regex.Match($orgProjectFile)
    $orgVersion     = $match.Groups[1].Value
    $tmpProjectFile = $orgProjectFile.Replace("<Version>$orgVersion</Version>", "<Version>$newVersion</Version>")

    Copy-Item "$projectPath" "$projectPath.org"
    
    $tmpProjectFile | Out-File -FilePath "$projectPath" -Encoding utf8

    if (-not (Test-Path "$publishPath")) {
        New-Item -Path "$publishPath" -ItemType Directory
    }

    dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Release -o "$publishPath"
   
    # NOTE: We're not using this because including source and symbols above because
    # doesn't seem to to work.
    #
	# dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NUGET_LOCAL_FEED"

    # Restore the project file.

    Copy-Item "$projectPath.org" "$projectPath"
    Remove-Item "$projectPath.org"
}

# Verify that the [NUGET_LOCAL_FEED] environment variable exists and references an
# existing folder that will act as our local nuget feed.

if ("$env:NUGET_LOCAL_FEED" -eq "") {
    echo "ERROR: [NUGET_LOCAL_FEED] environment variable does not exist."
    pause
    exit 1
}

if (-not (Test-Path "$env:NUGET_LOCAL_FEED")) {
    New-Item -Path "$env:NUGET_LOCAL_FEED" -ItemType Directory
}

# We're going to track the local minor version number at:
#
#       %NUGET_LOCAL_FEED%\next-version.txt
#
# This will include the integer value for the next preview tag and
# will be initialized to 0 when the file is not present.  We'll
# read this value and pass it to the publish function which will
# temporarily change the project's package version to:
#
#       10000.0.#-local
#
# where # is the next minor version number.  The function will
# publish the package and then restore the nuget version to its
# original value.  Using a preview tag will help ensure that 
# other projects won't reference any of these packages by accident.
#
# After publishing all of the packages, we'll increment the
# value in the version file.

$versionPath = [io.path]::combine($env:NUGET_LOCAL_FEED, "next-version.txt")

if (-not (Test-Path "$versionPath")) {
    "0" | Out-File -FilePath $versionPath -Encoding ASCII
}

$version = [int]::Parse($(Get-Content -Path "$versionPath" -First 1))

# Build and publish the projects.

Publish Neon.Cadence            $version
Publish Neon.Cassandra          $version
Publish Neon.Common             $version
Publish Neon.Couchbase          $version
Publish Neon.Cryptography       $version
Publish Neon.Docker             $version
Publish Neon.HyperV             $version
Publish Neon.Kube               $version
Publish Neon.Kube.Aws           $version
Publish Neon.Kube.Azure         $version
Publish Neon.Kube.BareMetal     $version
Publish Neon.Kube.Google        $version
Publish Neon.Kube.Hosting       $version
Publish Neon.Kube.HyperV        $version
Publish Neon.Kube.HyperVLocal   $version
Publish Neon.Kube.XenServer     $version
Publish Neon.Service            $version
Publish Neon.ModelGen           $version
Publish Neon.Nats               $version
Publish Neon.Postgres           $version
Publish Neon.SSH                $version
Publish Neon.SSH.NET            $version
Publish Neon.Temporal           $version
Publish Neon.Web                $version
Publish Neon.XenServer          $version
Publish Neon.Xunit              $version
Publish Neon.Xunit.Cadence      $version
Publish Neon.Xunit.Couchbase    $version
Publish Neon.Xunit.Kube         $version
Publish Neon.Xunit.Temporal     $version
Publish Neon.Xunit.YugaByte     $version
Publish Neon.YugaByte           $version

# Increment the minor version.

($version + 1).ToString() | Out-File -FilePath $versionPath -Encoding ASCII

pause
