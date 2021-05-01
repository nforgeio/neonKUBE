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

$scriptPath   = $MyInvocation.MyCommand.Path
$scriptFolder = [System.IO.Path]::GetDirectoryName($scriptPath)

Push-Location $scriptFolder

. ./github.actions.ps1

Pop-Location

#------------------------------------------------------------------------------
# INTERNAL USE ONLY:
#
# Returns the name of the authenticated GitHub user an throws an exception 
# when there isn't an active login.

function Get-GitHubUser
{
    # We're going to query for authentication status and extract
    # the current user name.  We're expecting that status output 
    # to look  like this:
    #
    #   github.com
    #     x Logged in to github.com as jefflill (C:\Users\devbot.JOBRUNNER\.config\gh/hosts.yml)
    #     x Git operations for github.com configured to use https protocol.
    #     x Token: *******************

    $result = $(& gh auth status)
    ThrowOnExitCode

    $posStart  = $result.IndexOf("github.com as")
    $posStart += "github.com as".Length
    $posEnd    = $result.IndexOf("()", $posStart)

    $user = $result.Substring($posStart, $posEnd - $posStart).Trim()

    if ([System.String]::IsNullOrEmpty($user))
    {
        throw "Unable to parse GitHub user name."
    }

    return $user
}

#------------------------------------------------------------------------------
# Creates a GitHub issue with the specified title, labels, and body and optionally
# appends the body to an existing open issue with the same title.
#
# ARGUMENTS:
#
#   repo            - specifies the target GitHub repo like: github.com/nforgeio/neonCLOUD
#   title           - specifies the issue title
#   body            - specifies the first issue comment
#   append          - optionally appends the body to an existing open
#                     issue with the same title
#   assignees       - optionally specifies a list of assignees
#   labels          - optionally specifies a map of label name/values
#   masterPassword  - optionally specifies the user's master 1Password
#
# REMARKS:
#
# This requires that the current user have a GITHUB_PAT available in their
# 1Password user folder.
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
        [bool]$append = $false,
        [Parameter(Mandatory=$false)]
        $assignees = $null,
        [Parameter(Mandatory=$false)]
        $labels = $null,
        [Parameter(Mandatory=$false)]
        [string]$masterPassword = $null
    )

    # Log into GitHub

    if (![System.String]::IsNullOrEmpty($masterPassword))
    {
        $GITHUB_PAT = GetSecretPassword "GITHUB_PAT" -masterPassword $masterPassword
    }
    else
    {
        $GITHUB_PAT = GetSecretPassword "GITHUB_PAT"
    }

    $GITHUB_PAT | gh auth login --with-token
    ThrowOnExitCode

    $user = Get-GitHubUser

    try
    {
        $number = -1

        if ($append)
        {
            # Query for any open issues authored by the authenticated user and
            # look for the first one that has the same title (if one exists).

            $json = $(& gh --repo $repo issue list --author $user --state open --json title,number --label --limit 1000)
            ThrowOnExitCode

            $list   = Convert-FromJson $json
            $number = -1

            ForEach ($issue in $list)
            {
                if ($issue.title -eq $title)
                {
                    $number = $issue.number
                    Break
                }
            }
        }

        if ($number -eq -1)
        {
            # Create a new issue.

            $assigneeOption = ""

            ForEach ($assignee in $assignees)
            {
                if ($assigneeOption.Length -gt 0)
                {
                    $assigneeOption += ","
                }

                $assigneeOption += "$assignee"
            }

            $labelOption = ""

            ForEach ($label in $labels)
            {
                if ($labelOption.Length -gt 0)
                {
                    $labelOption += ","
                }

                $labelOption += $label
            }

            # The last line of the output is the issue link.

            $output = $(gh --repo $repo issue create --title $title --body $body)
            ThrowOnExitCode

            $outputLines = $output.Split([System.Environment]::NewLine)
            $issueUri    = $outputLines | Select-Object -Last 1

            return $issueUri
        }
        else
        {
            # Append to an existing issue.  The last line of the output will be The
            # URI to the new comment in the existing issue.

            $output = $(gh --repo $repo issue comment $number --body $body)
            ThrowOnExitCode

            $outputLines = $output.Split([System.Environment]::NewLine)
            $commentUri  = $outputLines | Select-Object -Last 1

            return $commentUri
        }
    }
    finally
    {
        # Log out

        gh auth logout
        ThrowOnExitCode
    }
}
