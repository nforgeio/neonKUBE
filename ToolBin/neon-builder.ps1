#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neon-builder.ps1
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

# Performs a clean build of the neonKUBE solution and publishes binaries
# to the [$/build] folder.  This can also optionally build the neonKUBE
# Desktop installer.
#
# USAGE: pwsh -file ./neon-builder.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -tools        - Builds the command line tools
#       -codedoc      - Builds the code documentation
#       -all          - Builds with all of the options above

param 
(
    [switch]$tools   = $false,
    [switch]$codedoc = $false,
    [switch]$all     = $false,
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

# Initialize

if ($all)
{
    $tools   = $true
    $codedoc = $true
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
$nfRoot      = $env:NK_ROOT
$nfSolution  = "$nfRoot\neonKUBE.sln"
$nfBuild     = "$env:NK_BUILD"
$nfLib       = "$nfRoot\Lib"
$nfTools     = "$nfRoot\Tools"
$nfToolBin   = "$nfRoot\ToolBin"
$buildConfig = "-p:Configuration=$config"
$env:PATH   += ";$nfBuild"

$libraryVersion = $(& "$nfToolBin\neon-build" read-version "$nfLib\Neon.Common\Build.cs" NeonLibraryVersion)
ThrowOnExitCode

#------------------------------------------------------------------------------
# Publishes a .NET Core project to the repo's build folder.
#
# ARGUMENTS:
#
#   $projectPath    - The relative project folder PATH
#   $targetName     - Name of the target executable

function PublishCore
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$projectPath,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$targetName
    )

    Write-Info ""
    Write-Info "**************************************************************************"
    Write-Info "*** PUBLISH: $targetName"
    Write-Info "**************************************************************************"
    Write-Info ""

    # Ensure that the NK_BUILD folder exists:

    [System.IO.Directory]::CreateDirectory($nfBuild) | Out-Null

    # Locate the published output folder (note that we need to handle apps targeting different versions of .NET):

    $projectPath = [System.IO.Path]::Combine($nfRoot, $projectPath)

    $potentialTargets = @(
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net6.0-windows", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net6.0-windows10.0.17763.0", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net6.0", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net6.0", "win10-x64", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0-windows", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0-windows10.0.17763.0", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0", "win10-x64", "$targetName.dll")),
        $([System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "netcoreapp3.1", "$targetName.dll"))
    )

    $targetPath = $null

    foreach ($path in $potentialTargets)
    {
        if ([System.IO.File]::Exists($path))
        {
            $targetPath = $path
            Write-Output("*** Publish target exists at: $path")
            break
        }
        else
        {
            Write-Output("*** Publish target does not exist at: $path")
        }
    }

    if ([System.String]::IsNullOrEmpty($targetPath))
    {
        throw "Cannot locate publish folder for: $projectPath"
    }

    $targetFolder = [System.IO.Path]::GetDirectoryName($targetPath)

    # Copy the binary files to a new build folder subdirectory named for the target and
    # generate the batch file to launch the program.

    $binaryFolder = [System.IO.Path]::Combine($nfBuild, $targetName)

    if ([System.IO.Directory]::Exists($binaryFolder))
    {
        [System.IO.Directory]::Delete($binaryFolder, $true)
    }

    [System.IO.Directory]::CreateDirectory($binaryFolder) | Out-Null
    Copy-Item -Path "$targetFolder/*" -Destination $binaryFolder -Recurse

    $cmdPath = [System.IO.Path]::Combine($nfBuild, "$targetName.cmd")

    [System.IO.File]::WriteAllText($cmdPath, "@echo off`r`n")
    [System.IO.File]::AppendAllText($cmdPath, "%~dp0\$targetName\$targetName.exe %*`r`n")
}

#------------------------------------------------------------------------------
# Perform the operation.

Push-Cwd $nfRoot | Out-Null

$verbosity = "minimal"

try
{
    # Build the solution.

    if (-not $nobuild)
    {
        # We see somewhat random build problems when Visual Studio has the solution open,
        # so have the user close Visual Studio instances first.

        # $todo(jefflill): 
        #
        # I'm not sure if we still need this.  I believe we were running into trouble
        # building the neonKUBE CodeDoc using SHFB, which we're no longer using.

        # Ensure-VisualStudioNotRunning

        # Clear the NK_BUILD folder and delete any [bin] or [obj] folders
        # to be really sure we're doing a clean build.  I've run into 
        # situations where I've upgraded SDKs or Visual Studio and Files
        # left over from previous builds that caused build trouble.

        & $nfToolBin\neon-build clean "$nfRoot"
        ThrowOnExitCode

        # Clean and build the solution.

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           CLEAN SOLUTION                                ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        & "$msbuild" "$nfSolution" $buildConfig -t:Clean -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: CLEAN FAILED"
        }

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           RESTORE PACKAGES                              ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        dotnet restore

        & "$msbuild" "$nfSolution" -t:restore -verbosity:minimal

        if (-not $?)
        {
            throw "ERROR: RESTORE FAILED"
        }

        Write-Info ""
        Write-Info "*******************************************************************************"
        Write-Info "***                           BUILD SOLUTION                                ***"
        Write-Info "*******************************************************************************"
        Write-Info ""

        & "$msbuild" "$nfSolution" $buildConfig -restore -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }

        # The build generates source files like [.NETCoreApp,Version=v5.0.AssemblyAttributes.cs] within
        # project [obj] configuration subdirectorties.  This can result in duplicate attribute compiler
        # errors because Visual Studio seems to be including these files from all of the configuration
        # subfolders rather than just for the current build configuration.  This isn't reproducable for 
        # simple solutions, so we haven't reported this to MSFT.
        #
        # We mostly run into this issue after performing a script based RELEASE build and then go back 
        # and try to build DEBUG with Visual Studio.  The workaround is to simply remove all of these
        # generated files here.

        & $nfToolBin\neon-build clean-attr "$nfRoot"
        ThrowOnExitCode
    }

    # Build the Neon tools.

    if ($tools)
    {
        # Publish the Windows .NET Core tool binaries to the build folder.

        PublishCore "Tools\neon-cli\neon-cli.csproj" "neon"
        PublishCore "Tools\neon-modelgen\neon-modelgen.csproj" "neon-modelgen"
     }

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
            throw "ERROR: Cannot remove: $nfBuild\codedoc"
        }

        & "$msbuild" "$nfSolution" -p:Configuration=CodeDoc -restore -m -verbosity:$verbosity

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }

        # Copy the CHM file to a more convenient place for adding to the GitHub release
        # and generate the SHA256 for it.

        $nfDocOutput = "$nfroot\Websites\CodeDoc\bin\CodeDoc"

        & cp "$nfDocOutput\neon.chm" "$nfbuild"
        ThrowOnExitCode

        ""
        "Generating neon.chm SHA256..."
	    ""

        & cat "$nfBuild\neon.chm" | openssl dgst -sha256 -binary | neon-build hexdump > "$nfBuild\neon.chm.sha256.txt"
        ThrowOnExitCode

        # Move the documentation build output.
	
        & rm -r --force "$nfBuild\codedoc"
        ThrowOnExitCode

        & mv "$nfDocOutput" "$nfBuild\codedoc"
        ThrowOnExitCode

        # Munge the SHFB generated documentation site:
        #
        #   1. Insert the Google Analytics [gtag.js] scripts
        #   2. Munge and relocate HTML files for better site
        #      layout and friendlier permalinks.

        ""
        "Tweaking Layout and Enabling Google Analytics..."
	    ""

        & neon-build shfb --gtag="$nfroot\Websites\CodeDoc\gtag.js" --styles="$nfRoot\WebSites\CodeDoc\styles" "$nfRoot\WebSites\CodeDoc" "$nfBuild\codedoc"
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
