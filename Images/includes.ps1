#------------------------------------------------------------------------------
# FILE:         includes.ps1
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

#------------------------------------------------------------------------------
# Import the global project include file.

. $env:NF_ROOT/Powershell/includes.ps1

#------------------------------------------------------------------------------
# Important source code paths.

$src_path          = $env:NF_ROOT
$src_images_path   = "$src_path\\Images"
$src_lib_path      = "$src_path\\Lib"
$src_services_path = "$src_path\\Services"
$src_tools_path    = "$src_path\\Tools"

#------------------------------------------------------------------------------
# Global constants.

# neonKUBE cluster release Version.

$neonKUBE_Version = Get-Content "$env:NF_ROOT\neonKUBE-version.txt" -First 1

#------------------------------------------------------------------------------
# Executes a command, throwing an exception for non-zero error codes.

function Exec
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [scriptblock]$Command,
        [Parameter(Position=1, Mandatory=0)]
        [string]$ErrorMessage = "*** FAILED: $Command"
    )
    & $Command
    if ($LastExitCode -ne 0) {
        throw "Exec: $ErrorMessage"
    }
}

#------------------------------------------------------------------------------
# Deletes a file if it exists.

function DeleteFile
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item $Path 
	} 
}

#------------------------------------------------------------------------------
# Deletes a folder if it exists.

function DeleteFolder
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item -Recurse $Path 
	} 
}

#------------------------------------------------------------------------------
# Pushes a Docker image to the public registry with retry as an attempt to handle
# transient registry issues.
#
# Note that you may set [$noImagePush=$true] to disable image pushing for debugging
# purposes.  The [publish.ps1] scripts accept the [--nopush] switchto control this.

$noImagePush = $false

function PushImage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Image
    )

	if ($noImagePush)
	{
		return
	}

	$maxAttempts = 5

	for ($attempt=0; $attempt -lt $maxAttempts; $attempt++)
	{
		if ($attempt -gt 0)
		{
			"*** PUSH: RETRYING"
		}

		# $hack(jefflill):
		#
		# I'm seeing [docker push ...] write "blob upload unknown" messages to the
		# output and then it appears that the image manifest gets uploaded with no
		# layers.  The Docker Hub dashboard reports comppressed size as 0 for the
		# image/tag.  This appears to be transient because publishing again seems
		# to fix this.
		#
		# It appears that the "blob upload unknown" problem happens for misconfigured
		# Docker registries with multiple backends that do not share the same SECRET.
		# This should never happen for Docker Hub, but that's what we're seeing.
		# I wonder if this is due to a problem with their CDN (which is AWS CloudFront).
		# I'm seeing these problems for the first time (I think) during Thanksgiving
		# weekend (Black Friday and all) and I wonder if AWS has put CloudFront into
		# a special (less compatible) mode to handle all of the eCommerce traffic.
		#
		# The screwy thing is that [docker push] still appears to return a zero
		# exit code in this case.  I'm going to workaround this by using [Tee-Object]
		# to capture the [docker push] output and then look for this string:
		#
		#		"blob upload unknown"
		#
		# and then retry if we see this.
		#
		# An alternative (and cleaner) approach would be to actually examine the
		# repository as it appears in the remote registry to look for problems
		# there.  Perhaps this is something we could do after implementing [neon-cli]
		# registry commands.

		docker push "$Image" | Tee-Object -Variable pushOutput

		$exitCode = $LastExitCode

		if ($pushOutput -match 'blob upload unknown')
		{
			"*** PUSH: BLOB UPLOAD UNKNOWN"
			$exitCode = 100
		}

		if ($exitCode -eq 0)
		{
			return
		}
		
		"*** PUSH: EXITCODE=$exitCode"

		sleep 15
	}

	throw "[docker push $Image] failed after [$maxAttempts] attempts."
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
	$branch = GitBranch $env:NF_ROOT
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
    $branch = GitBranch $env:NF_ROOT

	return $rel -or ($branch -like "release-*")
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
	$branch = GitBranch $env:NF_ROOT

	return $rel -or ($branch -like "release-*") -or ($branch -eq "master")
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonLIBRARY GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".
#
# This default behavior can be overridden by setting the [$rel] or [$dev] Variable
# to $true.  These are generally passed as arguments to the root publish script.

function GetLibraryRegistry($image)
{
	if ($dev -and $rel)
	{
		'ERROR: $dev and $rel cannot both be $true.'
		exit 1
	}

	if ($dev)
	{
		return "ghcr.io/neonrelease-dev/" + $image
	}
	
	if ($rel)
	{
		return "ghcr.io/neonrelease/" + $image
	}

	if (IsRelease)
	{
		return "ghcr.io/neonrelease/" + $image
	}
	else
	{
		return "ghcr.io/neonrelease-dev/" + $image
	}
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonCLOUD GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".
#
# This default behavior can be overridden by setting the [$rel] or [$dev] Variable
# to $true.  These are generally passed as arguments to the root publish script.

function GetNeonCloudRegistry($image)
{
	if ($dev -and $rel)
	{
		'ERROR: $dev and $rel cannot both be $true.'
		exit 1
	}

	if ($dev)
	{
		return "ghcr.io/neonrelease-dev/" + $image
	}
	
	if ($rel)
	{
		return "ghcr.io/neonrelease/" + $image
	}

	if (IsRelease)
	{
		return "ghcr.io/neonrelease/" + $image
	}
	else
	{
		return "ghcr.io/neonrelease-dev/" + $image
	}
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonKUBE MAIN GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The MAIN registry
# holds the neonKUBE images tagged by cluster version.
#
# This default behavior can be overridden by setting the [$rel] or [$dev] Variable
# to $true.  These are generally passed as arguments to the root publish script.

function GetKubeMainRegistry($image)
{
	if ($dev -and $rel)
	{
		'ERROR: $dev and $rel cannot both be $true.'
		exit 1
	}

	if ($dev)
	{
		return "ghcr.io/neonkube-dev/" + $image
	}
	
	if ($rel)
	{
		return "ghcr.io/neonkube/" + $image
	}

	if (IsRelease)
	{
		return "ghcr.io/neonkube/" + $image
	}
	else
	{
		return "ghcr.io/neonkube-dev/" + $image
	}
}

#------------------------------------------------------------------------------
# Prefixes the image name passed with the target neonKUBE BASE GitHub container 
# registry for the current git branch by default such that when the current branch
# name starts with "release-" the image will be pushed to "ghcr.io/neonrelease/"
# otherwise it will be pushed to "ghcr.io/neonrelease-dev/".  The BASE registry
# holds the neonKUBE base images tagged with the component version.
#
# This default behavior can be overridden by setting the [$rel] or [$dev] Variable
# to $true.  These are generally passed as arguments to the root publish script.

function GetKubeBaseRegistry($image)
{
	if ($dev -and $rel)
	{
		'ERROR: $dev and $rel cannot both be $true.'
		exit 1
	}

	if ($dev)
	{
		return "ghcr.io/neonkube-base-dev/" + $image
	}
	
	if ($rel)
	{
		return "ghcr.io/neonkube-base/" + $image
	}

	if (IsRelease)
	{
		return "ghcr.io/neonkube-base/" + $image
	}
	else
	{
		return "ghcr.io/neonkube-base-dev/" + $image
	}
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
# Returns the neonKUBE MAIN registry organization corresponding to the current git branch.

function KubeMainRegistryOrg
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
        [Parameter(Position=0, Mandatory=1)] [string] $registry,
        [Parameter(Position=1, Mandatory=1)] [string] $tag
    )

	"   "
	"==========================================================="
	"* " + $registry + ":" + $tag
	"==========================================================="
}

#------------------------------------------------------------------------------
# Makes any text files that will be included in Docker images Linux safe, by
# converting CRLF line endings to LF and replacing TABs with spaces.

exec { unix-text --recursive $image_root\Dockerfile }
exec { unix-text --recursive $image_root\*.sh }
exec { unix-text --recursive .\*.cfg }
exec { unix-text --recursive .\*.js }
exec { unix-text --recursive .\*.conf }
exec { unix-text --recursive .\*.md }
exec { unix-text --recursive .\*.json }
exec { unix-text --recursive .\*.rb }
exec { unix-text --recursive .\*.py }
