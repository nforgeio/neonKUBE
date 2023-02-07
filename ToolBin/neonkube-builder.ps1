#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neonkube-builder.ps1
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

# Performs a clean build of the neonKUBE solution and publishes binaries
# to the [$/build] folder.
#
# USAGE: pwsh -f neonkube-builder.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -codedoc    - Builds the code documentation
#       -all        - Builds with all of the options above
#       -dirty      - Use GitHub sources for SourceLink even if local repo is dirty

param 
(
    [switch]$codedoc = $false,
    [switch]$all     = $false,
    [switch]$dirty   = $false,  # use GitHub sources for SourceLink even if local repo is dirty
    [switch]$debug   = $false   # Optionally specify DEBUG build config
)

#------------------------------------------------------------------------------
# $todo(jefflill):

if ($codedoc)
{
    Write-Error " "
    Write-Error "ERROR: Code documentation builds are temporarily disabled until we"
    Write-Error "       port to DocFX.  SHFB doesn't work for multi-targeted projects."
    Write-Error " "
    Write-Error "       https://github.com/nforgeio/neonKUBE/issues/1206"
    Write-Error " "
    exit 1
}

#------------------------------------------------------------------------------

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Abort if Visual Studio is running because that can cause [pubcore] to
# fail due to locked files.

# $note(jefflill): 
#
# We don't currently need this check but I'm leaving it here commented
# out to make it easier to revive in the future, if necessary.

# Ensure-VisualStudioNotRunning

# Initialize

if ($all)
{
    # $codedoc = $true
}

if ($debug)
{
    $config = "Debug"
}
else
{
    $config = "Release"
}

$msbuild     = $env:MSBUILDPATH
$nkRoot      = $env:NK_ROOT
$nkSolution  = "$nkRoot\neonKUBE.sln"
$nkBuild     = "$env:NK_BUILD"
$nkLib       = "$nkRoot\Lib"
$nkTools     = "$nkRoot\Tools"
$nkToolBin   = "$nkRoot\ToolBin"
$buildConfig = "-p:Configuration=$config"
$env:PATH   += ";$nkBuild"

$neonSdkVersion = $(& "neon-build" read-version "$nkLib\Neon.Kube\KubeVersions.cs" NeonKube)
ThrowOnExitCode

#------------------------------------------------------------------------------
# Perform the operation.

Push-Cwd $nkRoot | Out-Null

$verbosity = "minimal"

try
{
    #--------------------------------------------------------------------------
    # SourceLink configuration: We need to decide whether to set the environment variable 
    # [NEON_PUBLIC_SOURCELINK=true] to enable SourceLink references to our GitHub repos.

    $gitDirty = IsGitDirty

    if ($gitDirty -and -not $dirty)
    {
        throw "Cannot publish nugets because the git branch is dirty.  Use the [-dirty] option to override."
    }

    $env:NEON_PUBLIC_SOURCELINK = "true"

    #--------------------------------------------------------------------------
    # Build the solution.

    if (-not $nobuild)
    {
        # We see somewhat random build problems when Visual Studio has the solution open,
        # so have the user close Visual Studio instances first.

        # $note(jefflill): 
        #
        # We don't currently need this check but I'm leaving it here commented
        # out to make it easier to revive in the future, if necessary.

        # Ensure-VisualStudioNotRunning

        # Clear the NK_BUILD folder and delete any [bin] or [obj] folders
        # to be really sure we're doing a clean build.  I've run into 
        # situations where I've upgraded SDKs or Visual Studio and Files
        # left over from previous builds that caused build trouble.

        & neon-build clean "$nkRoot"
        ThrowOnExitCode

        # Clean and build the solution.

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           RESTORE PACKAGES                              ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        dotnet restore

        & dotnet restore "$nkSolution"

        if (-not $?)
        {
            throw "ERROR: RESTORE FAILED"
        }

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           CLEAN SOLUTION                                ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        & "$msbuild" "$nkSolution" $buildConfig -t:Clean -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: CLEAN FAILED"
        }

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           BUILD SOLUTION                                ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        & "$msbuild" "$nkSolution" $buildConfig -restore -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }
    }

    # Publish binaries.

    pubcore "$nkRoot\Tools\neon-cli\neon-cli.csproj" Release "$nkBuild/neon" win10-x64
    ThrowOnExitCode

    # Build the code documentation if requested.

    if ($codedoc)
    {
        Write-Info ""
        Write-Info "**********************************************************************"
        Write-Info "***                      CODE DOCUMENTATION                        ***"
        Write-Info "**********************************************************************"
        Write-Info ""

        # Remove some pesky aliases:

        del alias:rm
        del alias:cp
        del alias:mv

        if (-not $?)
        {
            throw "ERROR: Cannot remove: $nkBuild\codedoc"
        }

        & "$msbuild" "$nkSolution" -p:Configuration=CodeDoc -restore -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }

        # Move the documentation build output.
	
        & rm -r --force "$nkBuild\codedoc"
        ThrowOnExitCode

        & mv "$nkDocOutput" "$nkBuild\codedoc"
        ThrowOnExitCode

        # Munge the SHFB generated documentation site:
        #
        #   1. Insert the Google Analytics [gtag.js] scripts
        #   2. Munge and relocate HTML files for better site
        #      layout and friendlier permalinks.

        ""
        "Tweaking Layout and Enabling Google Analytics..."
	    ""

        & neon-build shfb --gtag="$nkroot\Websites\CodeDoc\gtag.js" --styles="$nkRoot\WebSites\CodeDoc\styles" "$nkRoot\WebSites\CodeDoc" "$nkBuild\codedoc"
        ThrowOnExitCode
    }
}
catch
{
    Write-Exception $_
    exit 1
}
finally
{
    Pop-Cwd | Out-Null
}
