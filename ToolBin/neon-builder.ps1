#Requires -Version 7.0
#------------------------------------------------------------------------------
# FILE:         neon-builder.ps1
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
    [switch]$all     = $false
)

if ($all)
{
    $tools   = $true
    $codedoc = $true
}

# Import the global project include file.

. $env:NF_ROOT/Powershell/includes.ps1

# Initialize

$msbuild        = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$nfRoot         = "$env:NF_ROOT"
$nfSolution     = "$nfRoot\neonKUBE.sln"
$nfBuild        = "$env:NF_BUILD"
$nfLib          = "$nfRoot\Lib"
$nfTools        = "$nfRoot\Tools"
$nfToolBin      = "$nfRoot\ToolBin"
$config         = "Release"
$buildConfig    = "-p:Configuration=Release"
$env:PATH      += ";$nfBuild"

$libraryVersion = $(& "$nfToolBin\neon-build" read-version "$nfLib\Neon.Common\Build.cs" NeonLibraryVersion)
ThrowOnExitCode

function PublishCore
{
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$projectPath,           # The relative project folder PATH

        [Parameter(Position=1, Mandatory=1)]
        [string]$targetName             # Name of the target executable
    )

    # Ensure that the NF_BUILD folder exists:

    [System.IO.Directory]::CreateDirectory($nfBuild)

    # Set the [pubcore] arguments (note that we need to handle apps targeting different versions of .NET):

    $projectPath = [System.IO.Path]::Combine($nfRoot, $projectPath)

    $targetPath = [System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0-windows", "$targetName.dll")
    
    if (!(Test-Path $targetPath))
    {
        $targetPath = [System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "net5.0", "$targetName.dll")

        if (!(Test-Path $targetPath))
        {
            $targetPath = [System.IO.Path]::Combine($ncRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "netcoreapp3.1", "$targetName.dll")

            if (!(Test-Path $targetPath))
            {
                Write-Error "Cannot locate publish folder for: $projectPath"
                Exit 1
            }
        }
    }

    # Publish the binaries.

    ""
    "**********************************************************"
    "*** PUBLISH: $targetName"
    "**********************************************************"
    ""
    "pubcore ""$projectPath"" ""$targetName"" ""$config"" ""$targetPath"" ""$nfBuild"" win10-x64"
    ""

    pubcore "$projectPath" "$targetName" "$config" "$targetPath" "$nfBuild" win10-x64

    if (-not $?)
    {
        ""
        "*** PUBLICATION FAILED ***"
        ""
        exit 1
    }
}

#------------------------------------------------------------------------------

# We see somewhat random build problems when Visual Studio has the solution open,
# so have the user close Visual Studio instances first.

Get-Process -Name devenv -ErrorAction SilentlyContinue | Out-Null

if ($?)
{
    Write-Error -Message "ERROR: Please close all Visual Studio instances before building."
    exit 1
}

$originalDir = $pwd
cd $nfRoot

# Build the solution.

if (-not $nobuild)
{
    # Clear the NF_BUILD folder and delete any [bin] or [obj] folders
    # to be really sure we're doing a clean build.  I've run into 
    # situations where I've upgraded SDKs or Visual Studio and Files
    # left over from previous builds that caused build trouble.

    & $nfToolBin\neon-build clean "$nfRoot"

    # Clean and build the solution.

    ""
    "**********************************************************"
    "***                   CLEAN SOLUTION                   ***"
    "**********************************************************"
    ""

    & "$msbuild" "$nfSolution" $buildConfig "-t:Clean"

    if (-not $?)
    {
        ""
        "*** CLEAN FAILED ***"
        ""
        exit 1
    }

    ""
    "**********************************************************"
    "***                   BUILD SOLUTION                   ***"
    "**********************************************************"
    ""

    & "$msbuild" "$nfSolution" $buildConfig -restore 

    if (-not $?)
    {
        ""
        "*** BUILD FAILED ***"
        ""
        exit 1
    }
}

# Build the Neon tools.

if ($tools)
{
    # Publish the Windows .NET Core binaries to the build folder.

    PublishCore "Tools\neon-cli\neon-cli.csproj"    "neon"
    PublishCore "Tools\unix-text\unix-text.csproj"  "unix-text"
    PublishCore "Tools\modelgen\modelgen.csproj"    "modelgen"

    # Hack to publish OS/X version of [neon-cli] to the build folder.

    ""
    "**********************************************************"
    "***                   OS/X neon-cli                    ***"
    "**********************************************************"
    ""

    cd "$nfTools\neon-cli"
    dotnet publish -r osx-x64 -c Release /p:PublishSingleFile=true
    & mkdir "$nfBuild\osx"
    & cp "$nfTools\neon-cli\bin\Release\netcoreapp3.1\osx-x64\publish\neon" "$nfBuild\osx\neon-osx"
    cd $nfRoot

    ""
    "Generating OS/X neon-cli SHA512..."
    ""

    & cat "$nfBuild\osx\neon-osx" | openssl dgst -sha512 -hex > "$nfBuild\osx\neon-osx-$desktopVersion.sha512.txt"

    if (-not $?)
    {
	    ""
	    "*** OS/X neon-cli: SHA512 failed ***"
	    ""
	    exit 1
    }
 }

# Build the code documentation if requested.

if ($codedoc)
{
    ""
    "**********************************************************"
    "***                CODE DOCUMENTATION                  ***"
    "**********************************************************"
    ""

    # Remove some pesky aliases:

    del alias:rm
    del alias:cp
    del alias:mv

    if (-not $?)
    {
        ""
        "*** ERROR: Cannot remove: $nfBuild\codedoc"
        ""
        exit 1
    }

    & "$msbuild" "$nfSolution" -p:Configuration=CodeDoc -restore

    if (-not $?)
    {
        ""
        "*** BUILD FAILED ***"
        ""
        exit 1
    }

    # Copy the CHM file to a more convenient place for adding to the GitHub release
    # and generate the SHA512 for it.

    $nfDocOutput = "$nfroot\Websites\CodeDoc\bin\CodeDoc"

    & cp "$nfDocOutput\neon.chm" "$nfbuild"

    ""
    "Generating neon.chm SHA512..."
	""

    & cat "$nfBuild\neon.chm" | openssl dgst -sha512 -hex > "$nfBuild\neon.chm.sha512.txt"

    # Move the documentation build output.
	
    & rm -r --force "$nfBuild\codedoc"
    & mv "$nfDocOutput" "$nfBuild\codedoc"

    if (-not $?)
    {
        ""
        "*** neon.chm: SHA512 failed ***"
        ""
        exit 1
    }

    # Munge the SHFB generated documentation site:
    #
    #   1. Insert the Google Analytics [gtag.js] scripts
    #   2. Munge and relocate HTML files for better site
    #      layout and friendlier permalinks.

    ""
    "Tweaking Layout and Enabling Google Analytics..."
	""

    & neon-build shfb --gtag="$nfroot\Websites\CodeDoc\gtag.js" --styles="$nfRoot\WebSites\CodeDoc\styles" "$nfRoot\WebSites\CodeDoc" "$nfBuild\codedoc"
}

cd $originalDir
