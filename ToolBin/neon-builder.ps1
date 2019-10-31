#------------------------------------------------------------------------------
# FILE:         neon-builder.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
# USAGE: powershell -file ./neon-builder.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -debug      - Builds the DEBUG version (this is the default)
#       -release    - Builds the RELEASE version
#       -nobuild    - Don't build the solution; just publish
#       -installer  - Builds installer binaries

param 
(
	[switch]$debug     = $false,
	[switch]$release   = $false,
	[switch]$nobuild   = $false,
	[switch]$installer = $false
)

$msbuild    = "C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\amd64\MSBuild.exe"
$nfRoot     = "$env:NF_ROOT"
$nfSolution = "$nfRoot\neonKUBE.sln"
$nfBuild    = "$env:NF_BUILD"
$nfTools    = "$nfRoot\Tools"
$env:PATH  += ";$nfBuild"
$version    = Get-Content "$env:NF_ROOT\product-version.txt" -First 1

if (-not $debug -and -not $release)
{
    $debug = $true
}

if ($debug)
{
    $config      = "Debug"
    $buildConfig = "-p:Configuration=Debug"
}
else
{
    $config      = "Release"
    $buildConfig = "-p:Configuration=Release"
}

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

    # Build the [pubcore] arguments:

    $projectPath = [System.IO.Path]::Combine($nfRoot, $projectPath)
    $targetPath  = [System.IO.Path]::Combine($nfRoot, [System.IO.Path]::GetDirectoryName($projectPath), "bin", $config, "netcoreapp3.0", "$targetName.dll")

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

$originalDir = $pwd
cd $nfRoot

# Copy the version from [$/product-version] into [$/Lib/Neon/Common/Build.cs]

& neon-build build-version

if (-not $nobuild)
{
    # Clear the NF_BUILD folder and delete any [bin] or [obj] folders
    # to be really sure we're doing a clean build.  I've run into 
    # situations where I've upgraded SDKs or Visual Studio and Files
    # left over from previous builds caused build trouble.

    & neon-build clean "$nfRoot"

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

# Publish the Windows .NET Core binaries to the build folder.

PublishCore "Tools\neon-cli\neon-cli.csproj"    "neon"
PublishCore "Tools\unix-text\unix-text.csproj"  "unix-text"

# Hack to publish OS/X version of [neon-cli] to the build folder.

""
"**********************************************************"
"***                   OS/X neon-cli                    ***"
"**********************************************************"
""

cd "$nfTools\neon-cli"
dotnet publish -r osx-x64 -c Release /p:PublishSingleFile=true
& mkdir "$nfBuild\osx"
& cp "$nfTools\neon-cli\bin\Release\netcoreapp3.0\osx-x64\publish\neon" "$nfBuild\osx\neon-osx"
cd $nfRoot

""
"Generating OS/X neon-cli sha512..."
""

& cat "$nfBuild\osx\neon-osx" | openssl dgst -sha512 -hex > "$nfBuild\osx\neon-osx-$version.sha512.txt"

if (-not $?)
{
	""
	"*** OS/X neon-cli: SHA512 failed ***"
	""
	exit 1
}
	
# Publish the WinDesktop binaries to the build folder.

 md -Force "$nfBuild\win-desktop"
 cp -R "$nfRoot\Desktop\WinDesktop\bin\Release\*" "$nfBuild\win-desktop"
 
 # Build the installer if requested.

if ($installer)
{
    ""
    "**********************************************************"
    "***            WINDOWS DESKTOP INSTALLER               ***"
    "**********************************************************"
    ""

    & neon-build installer windows

    if (-not $?)
    {
        ""
        "*** Windows Installer: Build failed ***"
        ""
        exit 1
    }

    ""
    "Generating windows installer sha512..."
	""
	
    & cat "$nfBuild\neonKUBE-setup-$version.exe" | openssl dgst -sha512 -hex > "$nfBuild\neonKUBE-setup-$version.sha512.txt"

    if (-not $?)
    {
        ""
        "*** Windows Installer: SHA512 failed ***"
        ""
        exit 1
    }
}

cd $originalDir
