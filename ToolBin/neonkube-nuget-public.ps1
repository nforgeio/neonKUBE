#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neon-nuget-public.ps1
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

# Publishes RELEASE builds of the NeonForge Nuget packages to the
# local file system and public Nuget.org repositories.

Write-Error "neonKUBE nuget publication is currently disabled."
exit 1

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

# Retrieve any necessary credentials.

$nugetApiKey = Get-SecretPassword "NUGET_PUBLIC_KEY"

#------------------------------------------------------------------------------
# Sets the package version in the specified project file.

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$project,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$version
    )

    "$project"
	neon-build pack-version NeonLibraryVersion "$env:NK_ROOT\Lib\$project\$project.csproj"
    ThrowOnExitCode
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

    $projectPath = [io.path]::combine($env:NK_ROOT, "Lib", "$project", "$project" + ".csproj")

	dotnet pack $projectPath -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o "$env:NK_BUILD\nuget"
    ThrowOnExitCode

    if (Test-Path "$env:NK_ROOT\Lib\$project\prerelease.txt")
    {
        $prerelease = Get-Content "$env:NK_ROOT\Lib\$project\prerelease.txt" -First 1
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

	nuget push -Source nuget.org -ApiKey $nugetApiKey "$env:NK_BUILD\nuget\$project.$libraryVersion$prerelease.nupkg" -SkipDuplicate -Timeout 600
    ThrowOnExitCode
}

# Load the library and neonKUBE versions.

$msbuild         = $env:MSBUILDPATH
$nkRoot          = "$env:NK_ROOT"
$nkSolution      = "$nkRoot\neonKUBE.sln"
$nkBuild         = "$env:NK_BUILD"
$nkLib           = "$nkRoot\Lib"
$nkTools         = "$nkRoot\Tools"
$nkToolBin       = "$nkRoot\ToolBin"
$libraryVersion  = $(& "neon-build" read-version "$nkLib/Neon.Common/Build.cs" NeonLibraryVersion)
$neonkubeVersion = $(& "neon-build" read-version "$nkLib/Neon.Kube/KubeVersions.cs" NeonKube)

# We need to do a release solution build to ensure that any tools or other
# dependencies are built before we build and publish the individual packages.

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

& "$msbuild" "$nkSolution" -p:Configuration=Release -restore -m -verbosity:quiet

if (-not $?)
{
    throw "ERROR: BUILD FAILED"
}

# Update the project versions.

SetVersion Neon.Kube                      $kubeVersion
SetVersion Neon.Kube.Aws                  $kubeVersion
SetVersion Neon.Kube.Azure                $kubeVersion
SetVersion Neon.Kube.BareMetal            $kubeVersion
SetVersion Neon.Kube.DesktopServer        $kubeVersion
SetVersion Neon.Kube.Google               $kubeVersion
SetVersion Neon.Kube.GrpcProto            $kubeVersion
SetVersion Neon.Kube.Hosting              $kubeVersion
SetVersion Neon.Kube.HyperV               $kubeVersion
SetVersion Neon.Kube.Models               $kubeVersion
SetVersion Neon.Kube.Operator             $kubeVersion
SetVersion Neon.Kube.ResourceDefinitions  $kubeVersion
SetVersion Neon.Kube.Resources            $kubeVersion
SetVersion Neon.Kube.Setup                $kubeVersion
SetVersion Neon.Kube.XenServer            $kubeVersion
SetVersion Neon.Kube.Xunit                $kubeVersion

# Build and publish the projects.

Publish Neon.Kube                         $kubeVersion
Publish Neon.Kube.Aws                     $kubeVersion
Publish Neon.Kube.Azure                   $kubeVersion
Publish Neon.Kube.BareMetal               $kubeVersion
Publish Neon.Kube.DesktopServer           $kubeVersion
Publish Neon.Kube.Google                  $kubeVersion
Publish Neon.Kube.GrpcProto               $kubeVersion
Publish Neon.Kube.Hosting                 $kubeVersion
Publish Neon.Kube.HyperV                  $kubeVersion
Publish Neon.Kube.Models                  $kubeVersion
Publish Neon.Kube.Operator                $kubeVersion
Publish Neon.Kube.ResourceDefinitions     $kubeVersion
Publish Neon.Kube.Resources               $kubeVersion
Publish Neon.Kube.Setup                   $kubeVersion
Publish Neon.Kube.XenServer               $kubeVersion
Publish Neon.Kube.Xunit                   $kubeVersion

# Remove all of the generated nuget files so these don't accumulate.

Remove-Item "$env:NK_BUILD\nuget\*"

""
"** Package publication completed"
""

