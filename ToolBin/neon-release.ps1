#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neon-release.ps1
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

# Performs neonKUBE release related functions.  Note that the 
# neon-builder.ps1 script must have been run successfully.
#
# USAGE: pwsh -file ./neon-release.ps1 [OPTIONS]
#
# OPTIONS:
#
#       -codedoc    - Releases the code documentation
#       -all        - Performs all of the options above

param 
(
    [switch]$codedoc = $false,
    [switch]$all     = $false
)

if ($all)
{
    $codedoc = $true
}

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Initialize

$msbuild          = $env:MSBUILDPATH
$nfRoot           = "$env:NK_ROOT"
$nfSolution       = "$nfRoot\neonKUBE.sln"
$nfBuild          = "$env:NK_BUILD"
$nfLib            = "$nfRoot\Lib"
$nfTools          = "$nfRoot\Tools"
$nfToolBin        = "$nfRoot\ToolBin"
$nfCodeDoc        = "$nfRoot\..\nforgeio.github.io"
$nfCadenceSamples = "$nfRoot\..\cadence-samples"
$env:PATH        += ";$nfBuild"
$libraryVersion   = $(& "$nfToolBin\neon-build" read-version "$nfLib\Neon.Common\Build.cs" NeonLibraryVersion)
$originalDir      = $pwd

# Publish the code documentation.

if ($codedoc)
{
    Write-Info ""
    Write-Info "**********************************************************"
    Write-Info "***                 CODE DOCUMENTATION                 ***"
    Write-Info "**********************************************************"
    Write-Info ""

    cd $nfCodeDoc

    # Verify that [$nfCodeDoc] actually references the local clone
    # of the [nforgeio/nforgeio.github.io] repository.

    if (-not (Test-Path "$nfCodeDoc\is-codedoc.txt"))
    {
        ""
        "*** [$nfCodeDoc] does not reference a clone of the the [nforgeio/nforgeio.github.io] repo."
        ""
        exit 1
    }

    # Verify that [$nfCadenceSamples] actually references the local clone
    # of the [nforgeio/cadence-samples.github.io] repository.

    if (-not (Test-Path "$nfCadenceSamples\is-cadence-samples.txt"))
    {
        ""
        "*** [$nfCadenceSamples] does not reference a clone of the the [nforgeio/cadence-samples.github.io] repo."
        ""
        exit 1
    }

    # Remove some pesky aliases:

    del alias:rm
    del alias:cp
    del alias:mv

    # Clean all generated files from [nforgeio.github.io] repo.
    #
    # NOTE: Don't remove these non-generated files and folders:
    #
    #   .git
    #   .vs
    #   images
    #   .gitignore
    #   CNAME
    #   is-codedoc.txt

    "Pulling [nforgeio.github.io]..."
    
    git pull

    "Cleaning [nforgeio.github.io]..."

    rm -r -f "$nfCodeDoc\fti"
    rm -r -f "$nfCodeDoc\icons"
    rm -r -f "$nfCodeDoc\scripts"
    rm -r -f "$nfCodeDoc\styles"
    rm -r -f "$nfCodeDoc\toc"
    rm -r -f "$nfCodeDoc\html"

    rm -f "$nfCodeDoc\index.html"
    rm -f "$nfCodeDoc\LastBuild.log"
    rm -f "$nfCodeDoc\search.html"
    rm -f "$nfCodeDoc\SearchHelp.aspx"
    rm -f "$nfCodeDoc\SearchHelp.inc.php"
    rm -f "$nfCodeDoc\SearchHelp.php"
    rm -f "$nfCodeDoc\Web.Config"
    rm -f "$nfCodeDoc\WebKI.xml"
    rm -f "$nfCodeDoc\WebTOC.xml"

    # Copy the generated CodeDoc site to the [nforgeio.github.io] repo.

    "Copying content to [nforgeio.github.io]..."

    cp -r "$nfBuild\codedoc\." "$nfCodeDoc\"

    # Remove any unnecessary files.
    
    "Removing unnecessary files from [nforgeio.github.io]..."

    rm -f LastBuild.log
    rm -f *.aspx
    rm -f *.php
    rm -f Web.Config

    # Push the changes to GitHub.

    "Commiting local changes..."
    git add --all
    git commit --all --message "RELEASE: $libraryVersion"

    "Pushing to origin..."
    git push
}

cd $originalDir
