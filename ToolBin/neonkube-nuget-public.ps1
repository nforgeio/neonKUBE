#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neon-nuget-public.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
#
# USAGE: pwsh -f neonsdk-nuget-public.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -dirty      - Use GitHub sources for SourceLink even if local repo is dirty
#       -restore    - Just restore the CSPROJ files after cancelling publish
#
# REMARKS:
#
# NOTE: The script writes the package publication version to:
#
#           $/build/nuget/version.txt
#

param 
(
    [switch]$dirty   = $false,    # use GitHub sources for SourceLink even if local repo is dirty
    [switch]$restore = $false     # Just restore the CSPROJ files after cancelling publish
)

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Abort if Visual Studio is running because that can lead to 
# build configuration conflicts because this script builds the
# RELEASE configuration and we normally have VS in DEBUG mode.

#Ensure-VisualStudioNotRunning

# Verify that the user has the required environment variables.  These will
# be available only for maintainers and are intialized by the NEONCLOUD
# [buildenv.cmd] script.

if (!(Test-Path env:NC_ROOT))
{
    "*** ERROR: This script is intended for maintainers only:"
    "           [NC_ROOT] environment variable is not defined."
    ""
    "           Maintainers should re-run the NEONCLOUD [buildenv.cmd] script."

    return 1
}

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
	neon-build pack-version "$env:NK_ROOT\Lib\Neon.Kube\KubeVersions.cs" NeonKube "$env:NK_ROOT\Lib\$project\$project.csproj"
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

    # Disabling symbol packages now that we're embedding PDB files.
    #
    # dotnet pack $projectPath -c Release -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg -o "$env:NK_BUILD\nuget"

    dotnet pack $projectPath -c Release -o "$env:NK_BUILD\nuget" -p:SolutionName=$env:SolutionName
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

	nuget push -Source nuget.org -ApiKey $nugetApiKey "$env:NK_BUILD\nuget\$project.$version.nupkg" -SkipDuplicate -Timeout 600
    ThrowOnExitCode
}

try
{
    if ([System.String]::IsNullOrEmpty($env:SolutionName))
    {
        $env:SolutionName = "neonKUBE"
    }

    $msbuild         = $env:MSBUILDPATH
    $neonBuild       = "$env:NF_ROOT\ToolBin\neon-build\neon-build.exe"
    $config          = "Release"
    $nkRoot          = "$env:NK_ROOT"
    $nkSolution      = "$nkRoot\neonKUBE.sln"
    $nkBuild         = "$env:NK_BUILD"
    $nkLib           = "$nkRoot\Lib"
    $nkTools         = "$nkRoot\Tools"
    $nkToolBin       = "$nkRoot\ToolBin"
    $neonSdkVersion  = $(& "neon-build" read-version "$nkLib/Neon.Common/Build.cs" NeonSdkVersion)
    $neonkubeVersion = $(& "neon-build" read-version "$nkLib/Neon.Kube/KubeVersions.cs" NeonKube)

    #------------------------------------------------------------------------------
    # Save the publish version to [$/build/nuget/version.text] so release tools can
    # determine the current release.

    [System.IO.Directory]::CreateDirectory("$nkRoot\build\nuget") | Out-Null
    [System.IO.File]::WriteAllText("$nkRoot\build\nuget\version.txt", $neonkubeVersion)

    #--------------------------------------------------------------------------
    # SourceLink configuration:
	#
	# We're going to fail this when the current git branch is dirty 
	# and [-dirty] wasn't passed.

    $gitDirty = IsGitDirty

    if ($gitDirty -and -not $dirty)
    {
        throw "Cannot publish nugets because the git branch is dirty.  Use the [-dirty] option to override."
    }

    $env:NEON_PUBLIC_SOURCELINK = "true"

    #--------------------------------------------------------------------------
    # We need to do a release solution build to ensure that any tools or other
    # dependencies are built before we build and publish the individual packages.

    if (-not $restore)
    {
        Write-Info ""
        Write-Info "********************************************************************************"
        Write-Info "***                            CLEAN SOLUTION                                ***"
        Write-Info "********************************************************************************"
        Write-Info ""

        Invoke-Program "`"$neonBuild`" clean `"$nkRoot`""

        Write-Info  ""
        Write-Info  "*******************************************************************************"
        Write-Info  "***                           BUILD SOLUTION                                ***"
        Write-Info  "*******************************************************************************"
        Write-Info  ""

        & "$msbuild" "$nkSolution" -p:Configuration=Release -t:restore,build -p:RestorePackagesConfig=true -m -verbosity:quiet

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }

        #------------------------------------------------------------------------------
        # Update the project versions.

        SetVersion Neon.Kube                      $neonkubeVersion
        SetVersion Neon.Kube.Aws                  $neonkubeVersion
        SetVersion Neon.Kube.Azure                $neonkubeVersion
        SetVersion Neon.Kube.BareMetal            $neonkubeVersion
        SetVersion Neon.Kube.BuildInfo            $neonkubeVersion
        SetVersion Neon.Kube.DesktopService       $neonkubeVersion
        SetVersion Neon.Kube.Google               $neonkubeVersion
        SetVersion Neon.Kube.GrpcProto            $neonkubeVersion
        SetVersion Neon.Kube.Hosting              $neonkubeVersion
        SetVersion Neon.Kube.HyperV               $neonkubeVersion
        SetVersion Neon.Kube.Models               $neonkubeVersion
        SetVersion Neon.Kube.Resources            $neonkubeVersion
        SetVersion Neon.Kube.Setup                $neonkubeVersion
        SetVersion Neon.Kube.XenServer            $neonkubeVersion
        SetVersion Neon.Kube.Xunit                $neonkubeVersion

        #------------------------------------------------------------------------------
        # Build and publish the projects.

        Publish Neon.Kube                         $neonkubeVersion
        Publish Neon.Kube.Aws                     $neonkubeVersion
        Publish Neon.Kube.Azure                   $neonkubeVersion
        Publish Neon.Kube.BareMetal               $neonkubeVersion
        Publish Neon.Kube.BuildInfo               $neonkubeVersion
        Publish Neon.Kube.DesktopService          $neonkubeVersion
        Publish Neon.Kube.Google                  $neonkubeVersion
        Publish Neon.Kube.GrpcProto               $neonkubeVersion
        Publish Neon.Kube.Hosting                 $neonkubeVersion
        Publish Neon.Kube.HyperV                  $neonkubeVersion
        Publish Neon.Kube.Models                  $neonkubeVersion
        Publish Neon.Kube.Setup                   $neonkubeVersion
        Publish Neon.Kube.XenServer               $neonkubeVersion
        Publish Neon.Kube.Xunit                   $neonkubeVersion
    }

    #------------------------------------------------------------------------------
    # Remove all of the generated nuget files so these don't accumulate.

    Remove-Item "$env:NK_BUILD\nuget\*.nupkg"

    ""
    "** Package publication completed"
    ""
}
catch
{
    Write-Exception $_
    exit 1
}

