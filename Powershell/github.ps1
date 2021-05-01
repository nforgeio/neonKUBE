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

    $results   = Invoke-CaptureStreams "gh auth status"
    $stderr    = $results.stderr
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
#   repo            - specifies the target GitHub repo like: github.com/nforgeio/neonCLOUD
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
# NOTE: The title and body MAY NOT include DOUBLE QUOTES.
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
        [string]$repo,
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

    if ($title.Contains("`""))
    {
        throw "GitHub issue [title] may not include double quotes."
    }

    if ($body.Contains("`""))
    {
        throw "GitHub issue [body] may not include double quotes."
    }

    $append = ![System.String]::IsNullOrEmpty($appendLabel)

    # Log into GitHub and obtain the GitHub user name.

    Import-GitHubCredentials
    Login-GitHubUser $env:GITHUB_PAT
    $user = Get-GitHubUser

    try
    {
        if ($append)
        {
            # Query for any open issues authored by the authenticated user and
            # look for the first one that has the same title (if one exists).

            $results = Invoke-CaptureStreams "gh --repo $repo issue list --author $user --state open --label $appendLabel --json title,number --limit 1000"
            $json    = $results.stdout
            $list    = ConvertFrom-Json $json
            $number  = -1

            ForEach ($issue in $list)
            {
                if ($issue.title -eq $title)
                {
                    $number = $issue.number
                    Break
                }
            }

            $append = $number -ne -1
        }

        if (!$append)
        {
            # Create a new issue.

            $assigneeValues = ""

            ForEach ($assignee in $assignees)
            {
                if ($assigneeValues.Length -gt 0)
                {
                    $assigneeValues += ","
                }

                $assigneeValues += "$assignee"
            }

            $labelValues = ""

            ForEach ($label in $labels)
            {
                if ($labelValues.Length -gt 0)
                {
                    $labelValues += ","
                }

                $labelValues += $label
            }

            # The first line of the output is the issue link.

            $command = "gh --repo $repo issue create --title `"$title`" --body `"$body`""

            if ($assigneeValues.Length -gt 0)
            {
                $command += " --assignee $assigneeValues"
            }

            if ($labelValues.Length -gt 0)
            {
                $command += " --label $labelValues"
            }

            $results     = Invoke-CaptureStreams $command
            $output      = $results.stdout
            $outputLines = ToLineArray($output)
            $issueUri    = $outputLines[0]

            return $issueUri
        }
        else
        {
            # Append to an existing issue.  The first line of the output will be The
            # URI to the new comment in the existing issue.

            $results     = Invoke-CaptureStreams "gh --repo $repo issue comment $number --body `"$body`""
            $output      = $results.stdout
            $outputLines = ToLineArray($output)
            $commentUri  = $outputLines[0]

            return $commentUri
        }
    }
    finally
    {
        Logout-GitHubUser
    }
}
