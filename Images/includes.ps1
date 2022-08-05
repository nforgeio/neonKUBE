#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         includes.ps1
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

# Publishes DEBUG builds of the NeonForge Nuget packages to the repo
# at http://nuget-dev.neoncloud.io so intermediate builds can be shared 
# by maintainers.
#
# NOTE: This is script works only for maintainers with proper credentials.

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

#------------------------------------------------------------------------------
# Important source code paths.

$NK_ROOT     = $env:NK_ROOT
$nfImages   = "$NK_ROOT\Images"
$nfLib      = "$NK_ROOT\Lib"
$nfServices = "$NK_ROOT\Services"
$nfTools    = "$NK_ROOT\Tools"

#------------------------------------------------------------------------------
# Global constants.

# neonKUBE release Version.

$neonKUBE_Version = $(& "$NK_ROOT\ToolBin\neon-build" read-version "$nfLib\Neon.Kube\KubeVersions.cs" NeonKube)
ThrowOnExitCode

$neonKUBE_Tag = "neonkube-" + $neonKUBE_Version

# Override the common image tag if the [NEON_CONTAINER_TAG_OVERRIDE] is defined.\
# This is used for development purposes.

$tagOverride = $env:NEON_CONTAINER_TAG_OVERRIDE

if (-not [System.String]::IsNullOrEmpty($tagOverride))
{
	$neonKUBE_Tag = $tagOverride
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
# Returns the current Git branch, date, and commit formatted as a Docker image tag
# along with an optional dirty branch indicator.

function ImageTag
{
	$branch = GitBranch $env:NK_ROOT
	$date   = UtcDate
	$commit = git log -1 --pretty=%h
	$tag    = "$branch-$date-$commit"

	# Disabling this for now.  The problem is that temporary files are being
	# created during the image builds which is making the Git repo look dirty
	# when it's actually not.  One solution will be to make sure that 
	# [.getignore] actually ignores all of these temp files.

	#if (IsDirty)
	#{
	#	$tag += "-dirty"
	#}

	return $tag
}

#------------------------------------------------------------------------------
# Returns $true if the current Git branch is considered to be a release branch.
# Branches with names starting with "release-" are always considered to be a
# RELEASE branch.

function IsRelease
{
    $branch = GitBranch $env:NK_ROOT

	return ($branch -like "release-*")
}

#------------------------------------------------------------------------------
# Returns $true if images built from the current Git branch should be tagged
# with [:latest] when pushed to Docker Hub.  This will return $true for any
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
# Prefixes the image name passed with the target neonLIBRARY GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".

function GetLibraryRegistry($image)
{
	$org = LibraryRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the neonLIBRARY registry organization corresponding to the current git branch.

function LibraryRegistryOrg
{
	if (IsRelease)
	{
		return "ghcr.io/neonrelease"
	}
	else
	{
		return "ghcr.io/neonrelease-dev"
	}
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonKUBE SETUP GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The MAIN registry
# holds the neonKUBE images tagged by cluster version.

function GetKubeSetupRegistry($image)
{
	$org = KubeSetupRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the neonKUBE SETUP registry organization corresponding to the current git branch.

function KubeSetupRegistryOrg
{
	if (IsRelease)
	{
		return "ghcr.io/neonkube"
	}
	else
	{
		return "ghcr.io/neonkube-dev"
	}
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonKUBE BASE GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The BASE registry
# holds the neonKUBE base images tagged with the component version.

function GetKubeBaseRegistry($image)
{
	$org = KubeBaseRegistryOrg
	
	return "$org/$image"
}

#------------------------------------------------------------------------------
# Returns the neonKUBE BASE registry organization corresponding to the current git branch.

function KubeBaseRegistryOrg
{
	if (IsRelease)
	{
		return "ghcr.io/neonkube-base"
	}
	else
	{
		return "ghcr.io/neonkube-base-dev"
	}
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
# Returns the neonCLOUD registry organization corresponding to the current git branch.

function NeonCloudRegistryOrg
{
	if (IsRelease)
	{
		return "ghcr.io/neonrelease"
	}
	else
	{
		return "ghcr.io/neonrelease-dev"
	}
}

#------------------------------------------------------------------------------
# Returns $true if the current Git branch is includes uncommited changes or 
# untracked files.  This was inspired by this article:
#
#	http://remarkablemark.org/blog/2017/10/12/check-git-dirty/

function IsDirty
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

unix-text --recursive $image_root\Dockerfile 
unix-text --recursive $image_root\*.sh 
unix-text --recursive $image_root\*.cfg 
unix-text --recursive $image_root\*.js 
unix-text --recursive $image_root*.conf 
unix-text --recursive $image_root\*.md 
unix-text --recursive $image_root\*.json 
unix-text --recursive $image_root\*.rb 
unix-text --recursive $image_root\*.py 
