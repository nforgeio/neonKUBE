#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         includes.ps1
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

# This file includes common veriably definitions and functions used while
# building and publishing neonSDK nuget packages.
#
# NOTE: This is script works only for maintainers with proper credentials.

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

#------------------------------------------------------------------------------
# Important source repo paths.

$nfRoot     = $env:NF_ROOT
$nfImages   = "$nfRoot\Images"
$nfLib      = "$nfRoot\Lib"
$nfServices = "$nfRoot\Services"
$nfTools    = "$nfRoot\Tools"

$nkRoot     = $env:NK_ROOT
$nkImages   = "$nkRoot\Images"
$nkLib      = "$nkRoot\Lib"
$nkServices = "$nkRoot\Services"
$nkTools    = "$nkRoot\Tools"

$ncRoot     = $env:NC_ROOT
$ncImages   = "$ncRoot\Images"
$ncLib      = "$ncRoot\Lib"
$ncServices = "$ncRoot\Services"
$ncTools    = "$ncRoot\Tools"

#------------------------------------------------------------------------------
# Global constants.

# NEONKUBE cluster release version.

$neonKUBE_Version = $(& neon-build read-version "$nkRoot\Lib\Neon.Kube\KubeVersions.cs" NeonKube)
ThrowOnExitCode

# NEONKUBE container image tag.
#
# Note that we determine the currently checked-out Git branch for local NEONKUBE 
# repo.  If that's a release branch, then we'll just use the NEONKUBE version,
# otherwise, we'll append the branch name with a leading period to the tag.
#
# This helps to isolate container images between different branches so developers 
# can work on different cluster images in parallel.

$neonKUBE_Tag   = "neonkube-" + $neonKUBE_Version
$neonKubeBranch = GitBranch $env:NK_ROOT

if (-not $neonKubeBranch.StartsWith("release-"))
{
	$neonKUBE_Tag = "$neonKUBE_Tag.$neonKubeBranch"
}

#------------------------------------------------------------------------------
# Deletes a file if it exists.

function DeleteFile
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item $Path 
	} 
}

#------------------------------------------------------------------------------
# Returns the current date (UTC) formatted as "yyyyMMdd".

function UtcDate
{
	return [datetime]::UtcNow.ToString('yyyyMMdd')
}

#------------------------------------------------------------------------------
# Returns the .NET runtime base container reference to be used when building
# our container images, like:
#
#		mcr.microsoft.com/dotnet/aspnet:7.0.2-jammy-amd64
#
# This accepts a single parameter specifying the path to the [global.json] file
# used to control which .NET SDK we're using to build the container binaries.
# This uses the [neon-build dotnet-version] command to identify the runtime
# version and inject that into the base container name returned.

function Get-DotnetBaseImage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$globalJsonPath
    )

	$command  = "neon-build dotnet-version "  + '"' + $globalJsonPath + '"'
	$response = Invoke-CaptureStreams $command
	$lines    = $response.stdout -split '\r?\n'
	$runtime  = $lines[0].Trim()

	return "mcr.microsoft.com/dotnet/aspnet:$runtime-jammy-amd64"
}

#------------------------------------------------------------------------------
# Returns $true if images built from the current Git branch should be tagged
# with [:latest] when pushed to Docker Hub.  This will return [$true] for any
# release branch starting with "release-" as well as the MASTER branch.
#
# This has the effect of tagging release builds with [:latest] in [ghcr.io/neonrelease]
# for release branches and MASTER branch builds with [:lasest] in [ghcr.io/neonrelease-dev].

function TagAsLatest
{
	$branch = GitBranch $env:NK_ROOT

	return ($branch -like "release-*") -or ($branch -eq "master")
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonSDK GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".

function GetSdkRegistry($image)
{
	# $todo(jefflill):
	#
	# For now, we're going to use the neonkube image repo for all images because
	# the publish scripts in the other repos can't handle multiple image repos yet.

	return GetKubeStageRegistry $image
}

#------------------------------------------------------------------------------
# Returns the neonSDK registry organization corresponding to the current git branch.

function SdkRegistryOrg
{
	# $todo(jefflill):
	#
	# For now, we're going to use the neonkube image repo for all images because
	# the publish scripts in the other repos can't handle multiple image repos yet.

	return KubeStageRegistryOrg
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target NEONKUBE SETUP GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The MAIN registry
# holds the NEONKUBE images tagged by cluster version.

function GetKubeStageRegistry($image)
{
	$org = KubeStageRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the NEONKUBE staging container image registy.

function KubeStageRegistryOrg
{
	return "ghcr.io/neonkube-stage"
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target NEONKUBE BASE GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The BASE registry
# holds the NEONKUBE base images tagged with the component version.

function GetKubeBaseRegistry($image)
{
	$org = KubeBaseRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the NEONKUBE staging base container image registry.

function KubeBaseRegistryOrg
{
	return "ghcr.io/neonkube-base-dev"
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonCLOUD GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".

function GetNeonCloudRegistry($image)
{
	$org = NeonCloudRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the neonCLOUD container image registry .

function NeonCloudRegistryOrg
{
	return "ghcr.io/neonrelease-dev"
}

#------------------------------------------------------------------------------
# Returns $true if the current Git branch is includes uncommited changes or 
# untracked files.  This was inspired by this article:
#
#	http://remarkablemark.org/blog/2017/10/12/check-git-dirty/

function IsGitDirty
{
	$check = git status --short

	if (!$check)
	{
		return $false
	}

	if ($check.Trim() -ne "")
	{
		return $true
	}
	else
	{
		return $false
	}
}

#------------------------------------------------------------------------------
# Writes text to STDOUT that marks the beginning on a Docker image build. 

function Log-ImageBuild
{
    [CmdletBinding()]
    param 
	(
        [Parameter(Position=0, Mandatory=$true)] [string] $registry,
        [Parameter(Position=1, Mandatory=$true)] [string] $tag
    )

	$image = $registry + ":" + $tag

	Write-Info " "
	Write-Info "==============================================================================="
	Write-Info "* Building: $image"
	Write-Info "==============================================================================="
	Write-Info " "
}

#------------------------------------------------------------------------------
# Makes any text files that will be included in Docker images Linux safe by
# converting CRLF line endings to LF and replacing TABs with spaces.

unix-text --recursive $image_root\*.sh 
unix-text --recursive $image_root\*.cfg 
unix-text --recursive $image_root\*.js 
unix-text --recursive $image_root*.conf 
unix-text --recursive $image_root\*.md 
unix-text --recursive $image_root\*.json 
unix-text --recursive $image_root\*.rb 
unix-text --recursive $image_root\*.py 
