#Requires -Version 7.0
#------------------------------------------------------------------------------
# FILE:         github.ps1
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
# IMPORTANT:
#
# This file defines GitHub related Powershell functions and is intended for use
# in GitHub actions and other deployment related scenarios.  This file is intended
# to be shared/included across multiple GitHub repos and should never include
# repo-specific code.
#
# After modifying this file, you should take care to push any changes to the
# other repos where this file is present.

Import-Module Microsoft.PowerShell.Utility

$scriptPath   = $MyInvocation.MyCommand.Path
$scriptFolder = [System.IO.Path]::GetDirectoryName($scriptPath)

Push-Location $scriptFolder

. ./error-handling.ps1
. ./utility.ps1
. ./deployment.ps1
. ./github.actions.ps1

Pop-Location

#------------------------------------------------------------------------------
# Call this after native commands to check for non-zero exit codes.

function ThrowOnExitCode 
{
    if ($LastExitCode -ne 0)
    {
        throw "ERROR: exitcode=$LastExitCode"
    }
}

#------------------------------------------------------------------------------
# Logs into GitHub using a GITHUB_PAT.
#
# ARGUMENTS:
#
#   github_pat      - the GITHUB_PAT

function Login-GitHubUser
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$github_pat
    )

    $github_pat | gh auth login --with-token
    ThrowOnExitCode
}

#------------------------------------------------------------------------------
# Logs out of GitHub.

function Logout-GitHubUser
{
    Write-Output "Y" | gh auth logout --hostname github.com
}

#------------------------------------------------------------------------------
# Returns the name of the authenticated GitHub user an throws an exception 
# when there isn't an active login.

function Get-GitHubUser
{
    # We're going to query for authentication status and extract
    # the current user name.  We're expecting that stderr output 
    # to look  like this:
    #
    #   github.com
    #     x Logged in to github.com as jefflill (C:\Users\devbot.JOBRUNNER\.config\gh/hosts.yml)
    #     x Git operations for github.com configured to use https protocol.
    #     x Token: *******************

    $result    = Invoke-CaptureStreams "gh auth status"
    $stderr    = $result.stderr
    $posStart  = $stderr.IndexOf("github.com as")
    $posStart += "github.com as".Length
    $posEnd    = $stderr.IndexOf("(", $posStart)

    $user = $stderr.Substring($posStart, $posEnd - $posStart).Trim()

    if ([System.String]::IsNullOrEmpty($user))
    {
        throw "Unable to parse GitHub user name."
    }

    return $user
}

#------------------------------------------------------------------------------
# Creates a GitHub issue with the specified title, labels, and body and optionally
# appends the body to an existing open issue with the same author, title and 
# specified label.
#
# ARGUMENTS:
#
#   repoPath        - specifies the target GitHub repo like: github.com/nforgeio/neonCLOUD
#   title           - specifies the issue title
#   body            - specifies the first issue comment
#   appendLabel     - optionally enables appending to an existing issue
#                     (see the remarks for mor details)
#   assignees       - optionally specifies a list of assignees
#   labels          - optionally specifies a map of label name/values
#   masterPassword  - optionally specifies the user's master 1Password
#
# REMARKS:
#
# NOTE: This requires that the current user have a GITHUB_PAT available in their
#       1Password user folder.
#
# You can use the [appendLabel] parameter to append the body to an existing
# issue as a new comment.  Pass [appendLabel] as the issue label that will
# be combined with the current GitHub user and title to look for matching
# open issues.
#
# RETURNS:
#
# This function returns the URI to new issues and the URI to the new comment
# appended to an existing issue.

function New-GitHubIssue
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repoPath,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$title,
        [Parameter(Position=2, Mandatory=$true)]
        [string]$body,
        [Parameter(Mandatory=$false)]
        [string]$appendLabel = $null,
        [Parameter(Mandatory=$false)]
        $assignees = $null,
        [Parameter(Mandatory=$false)]
        $labels = $null,
        [Parameter(Mandatory=$false)]
        $masterPassword = $null
    )

    $repoDetails = Parse-GitHubRepoPath $repoPath
    $owner       = $repoDetails.owner
    $repo        = $repoDetails.repo

    # Log into GitHub and obtain the GitHub user name.

    Import-GitHubCredentials
    Login-GitHubUser $env:GITHUB_PAT

    $user = Get-GitHubUser

    # Detect when appending is enabled.

    $append = ![System.String]::IsNullOrEmpty($appendLabel)

    try
    {
        if ($append)
        {
            # Query for any open issues authored by the authenticated user and
            # look for the first one that has the same title (if one exists).

            $result      = Invoke-CaptureStreams "gh --repo $repoPath issue list --author $user --state open --label $appendLabel --json title,number --limit 1000"
            $json        = $result.stdout
            $list        = ConvertFrom-Json $json
            $issueNumber = -1

            ForEach ($issue in $list)
            {
                if ($issue.title -eq $title)
                {
                    $issueNumber = $issue.number
                    Break
                }
            }

            $append = $issueNumber -ne -1
        }

        if (!$append)
        {
            #------------------------------------------------------------------
            # Create a new issue.

            $request        = @{}
            $request.accept = "application/vnd.github.v3+json"
            $request.owner  = $owner
            $request.repo   = $repo
            $request.title  = $title
            $request.body   = $body

            # Initialize any assignees.

            $request.assignees = @()

            ForEach ($assignee in $assignees)
            {
                $request.assignees += $assignee
            }

            # Initialize any labels.

            $request.labels = @()

            ForEach ($label in $labels)
            {
                $request.labels += $label
            }

            # Use the REST API to create the issue.

            $result   = Invoke-GitHubApi "/repos/$owner/$repo/issues" "POST" -body $request
            $issueUri = $result.html_url

            return "$issueUri"
        }
        else
        {
            # Append a comment to an existing issue.

            $request              = @{}
            $request.accept       = "application/vnd.github.v3+json"
            $request.owner        = $owner
            $request.repo         = $repo
            $request.issue_number = $issueNumber
            $request.body         = $body

            $result     = Invoke-GitHubApi "/repos/$owner/$repo/issues/$issueNumber/comments" "POST" -body $request
            $commentUri = $result.html_url

            return $commentUri
        }
    }
    finally
    {
        Logout-GitHubUser
    }
}

#------------------------------------------------------------------------------
# Submits a REST API request to the GitHub via the CLI.
#
# ARGUMENTS:
#   
#   endpoint    - specifies the API endpoint
#   method      - Specifies the HTTP method (GET, POST, PUT, DELETE,...)
#   body        - optionally specifies the request body as a hasthtable
#
# RETURNS:
#
#   The API results as a hash table.
#
# REMARKS:
#
# NOTE: The GitHub client needs to already be authenticated.

function Invoke-GitHubApi
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$endpoint,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$method,
        [Parameter(Mandatory=$false)]
        [hashtable]$body = $null
    )

    # Generate a unique temporary file that will be used to hold the request body.

    if ($null -ne $body)
    {
        $bodyGuid = [System.Guid]::NewGuid().ToString("d")
        $bodyPath = [System.IO.Path]::Combine($env:TMP, "$bodyGuid.github.json")
    }

    try
    {
        # Submit the request.

        $command = "gh api $endpoint --method $method"

        if ($null -ne $body)
        {
            $json = $body | ConvertTo-Json
            [System.IO.File]::WriteAllText($bodyPath, $json)

            $command += " --input `"$bodyPath`""
        }

        $result = Invoke-CaptureStreams $command -NoCheck

        if ($result.exitCode -ne 0)
        {
            $exitcode = $result.exitcode
            $stdout   = $result.stdout
            $stderr   = $result.stderr

            throw "Invoke-GitHubApi Failed: [exitcode=$exitCode]`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
        }

        return ConvertFrom-Json $result.stdout
    }
    finally
    {
        if (($null -ne $body) -and [System.IO.File]::Exists($bodyPath))
        {
            [System.IO.File]::Delete($bodyPath)
        }
    }
}

#------------------------------------------------------------------------------
# Parses a repo path into its parts.
#
# ARGUMENTS:
#
#   repo        - the repo path, like: github.com/nforgeio/neonCLOUD
#
# RETURNS:
#
#   A hash table with these properties:
#
#       server      - the server part (github.com)
#       owner       - the organization part (nforgeio)
#       repo        - the repository name (neonCLOUD)

function Parse-GitHubRepoPath
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo
    )

    $parts = $repo.Split("/")
    
    $result        = @{}
    $result.server = $parts[0]
    $result.owner  = $parts[1]
    $result.repo   = $parts[2]

    return $result
}

