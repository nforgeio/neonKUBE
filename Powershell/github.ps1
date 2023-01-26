#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         github.ps1
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

Push-Location $scriptFolder | Out-Null

. ./error-handling.ps1
. ./utility.ps1
. ./deployment.ps1

Pop-Location | Out-Null

#------------------------------------------------------------------------------
# Returns $true if the current Git branch is includes uncommited changes or 
# untracked files.  This was inspired by this article:
#
#	http://remarkablemark.org/blog/2017/10/12/check-git-dirty/

function IsGitDirty
{
    Push-Location $scriptFolder | Out-Null
    $check = git status --short
    Pop-Location | Out-Null

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
#   repo           - specifies the target GitHub repo like: github.com/nforgeio/neonCLOUD
#   title           - specifies the issue title
#   body            - specifies the first issue comment
#   appendLabel     - optionally enables appending to an existing issue
#                     (see the remarks for mor details)
#   assignees       - optionally specifies a list of assignees
#   labels          - optionally specifies a hash table of label nameS
#   masterPassword  - optionally specifies the user's master 1Password
#
# REMARKS:
#
# NOTE: This requires that the current user have a GITHUB[accessto0ken] available 
#       in their 1Password user vault.
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

    $repoDetails = Parse-GitHubRepo $repo
    $owner       = $repoDetails.owner
    $repoName    = $repoDetails.repoName

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

            $result      = Invoke-CaptureStreams "gh --repo $repo issue list --author $user --state open --label $appendLabel --json title,number --limit 1000"
            $json        = $result.stdout
            $list        = $(ConvertFrom-Json $json -AsHashTable)
            $issueNumber = -1

            foreach ($issue in $list)
            {
                if ($issue.title -eq $title)
                {
                    $issueNumber = $issue.number
                    break
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

            foreach ($assignee in $assignees)
            {
                $request.assignees += $assignee
            }

            # Initialize any labels.

            $request.labels = @()

            foreach ($label in $labels)
            {
                $request.labels += $label
            }

            # Use the REST API to create the issue.

            $result   = Invoke-GitHubApi "/repos/$owner/$repoName/issues" "POST" -body $request
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

            $result     = Invoke-GitHubApi "/repos/$owner/$repoName/issues/$issueNumber/comments" "POST" -body $request
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

        return $(ConvertFrom-Json $result.stdout -AsHashTable)
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
#   repo        - the repo path, like: [github.com/]nforgeio/neonCLOUD
#
# RETURNS:
#
#   A hash table with these properties:
#
#       server      - the server part (github.com)
#       owner       - the organization part (nforgeio)
#       repoName    - the repository name (neonCLOUD)

function Parse-GitHubRepo
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo
    )

    $parts = $repo.Split("/")
    
    $result = @{}

    switch ($parts.Length)
    {
        2
        {
            $result.server   = "github.com"
            $result.owner    = $parts[0]
            $result.repoName = $parts[1]
        }

        3
        {
            $result.server   = $parts[0]
            $result.owner    = $parts[1]
            $result.repoName = $parts[2]        
        }

        default
        {
            throw "Invalid repo path: $repo"
        }
    }

    return $result
}

#==============================================================================
# GitHub Action utilities                                                     #
#==============================================================================

#------------------------------------------------------------------------------
# Returns the URI for the executing workflow.
#
# REMARKS:
#
# This currently assumes that all workflow YAML files are located within
# [$/.github/worklows/] and that they are named like: *.yaml

function Get-ActionWorkflowUri
{
    $workflowFileName = $env:GITHUB_WORKFLOW + ".yaml"

    # Extract the repo branch from GITHUB_REF. This includes the branch like:
    #
    #       refs/heads/master
    #
    # We'll extract the branch part after the last "/".

    $githubRef      = $env:GITHUB_REF
    $lastSlashPos   = $githubRef.LastIndexOf("/")
    $workflowBranch = $githubRef.Substring($lastSlashPos + 1)

    return "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/blob/$workflowBranch/.github/workflows/$workflowFileName"
}

#------------------------------------------------------------------------------
# Returns the URI for the executing workflow run.

function Get-ActionWorkflowRunUri
{
    return "$env:GITHUB_SERVER_URL/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
}

#------------------------------------------------------------------------------
# Escapes a potentially multi-line string such that it can be written to STDOUT
# and be processed correctly by the GitHub runner.
#
# ARGUMENTS:
#
#   value   - the string value being escaped
#
# RETURNS:
#
#  The escaped string

function Escape-ActionString
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$false)]
        [string]$value = $null
    )

    if ($value -eq $null)
    {
        return ""
    }

    $value = $value.Replace("%", "%25")     # We need to escape "%" too
    $value = $value.Replace("`r", "%0D")
    $value = $value.Replace("`n", "%0A")

    return $value
}

#------------------------------------------------------------------------------
# Writes a line of text (encoded as UTF-8) to the action output.  Use this instead
# of [Write-Output] because Powershell doesn't default to UTF-8 and its support
# for configuring the output encoding appears to be buggy.
#
# ARGUMENTS:
#
#   text        - the string being written or $null to write an empty line.
#   color       - optionally specifies the text color (one of: 'red' or 'yellow')
#   noEscape    - optional skip escaping the output string

function Write-ActionOutput
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$false)]
        [string]$text = $null,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$color = $null,
        [Parameter(Position=2, Mandatory=$false)]
        [switch]$noEscape = $false
    )

    if ($text -eq $null)
    {
        $text = ""
    }

    if (!$noEscape)
    {
        $text = Escape-ActionString $text
    }

    if (![System.String]::IsNullOrEmpty($text))
    {
        switch ($color)
        {
            "red"
            {
                $text = "`u{001b}[31m" + $text + "`u{001b}[0m"
            }

            "yellow"
            {
                $text = "`u{001b}[33m" + $text + "`u{001b}[0m"
            }

            $null
            {
            }

            ""
            {
            }

            default
            {
                throw "[$color]: is not a supported color."
            }
        }
    }

    Write-Output $text
}

#------------------------------------------------------------------------------
# Sets an action output value.
#
# ARGUMENTS:
#
#   name    - the action output name
#   value   - the value to be set (cannot be $null or empty)

function Set-ActionOutput
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$false)]
        [AllowEmptyString()]
        [string]$value = $null
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[$name] cannot be null or empty."
    }

    Write-ActionOutput "::set-output name=$name::$value"
}

#------------------------------------------------------------------------------
# Logs a debug message.  You must create a secret named ACTIONS_STEP_DEBUG with
# the value true to see the debug messages set by this command in the log. 
#
# For more information, see: 
#
#   https://docs.github.com/en/actions/managing-workflow-runs/enabling-debug-logging
#
# ARGUMENTS:
#
#   message     - the message

function Log-ActionDebugMessage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowEmptyString()]
        [string]$message
    )

    Write-ActionOutput "::debug::$message"
}

#------------------------------------------------------------------------------
# Writes a warning message to the action output.
#
# ARGUMENTS:
#
#   message     - the message

function Write-ActionWarning
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowEmptyString()]
        [string]$message
    )

    Write-ActionOutput $message "yellow"
}

#------------------------------------------------------------------------------
# Writes an error message to the action output.
#
# ARGUMENTS:
#
#   message     - the message
#   noEscape    - optionally skips escaping the output

function Write-ActionError
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowEmptyString()]
        [string]$message,
        [Parameter(Position=2, Mandatory=$false)]
        [switch]$noEscape = $false
    )

    Write-Output $message 
}

#------------------------------------------------------------------------------
# Opens an expandable group in the GitHub action output.
#
# ARGUMENTS:
#
#   groupTitle  - the group title

function Open-ActionOutputGroup
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$groupTitle
    )

    Write-ActionOutput "::group::$groupTitle"
}

#------------------------------------------------------------------------------
# Closes the current expandable group in the GitHub action output.

function Close-ActionOutputGroup
{
    Write-ActionOutput "::endgroup::"
}

#------------------------------------------------------------------------------
# Writes information about a Powershell action exception to the action output.
#
# ARGUMENTS:
#
#   error   - The error caught in a catch block via the automatic
#             [$_] or [$PSItem] variable

function Write-ActionException
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        $error
    )

    Write-Exception $error
}

#------------------------------------------------------------------------------
# Writes the contents of a text file to the action output, optionally nested
# within an action group.
#
# ARGUMENTS:
#
#   path                - path to the text file
#   groupTitle          - optionally specifies the group title
#   type                - optionally specifies the log file type fgor colorization.
#                         Pass one of these values:
#
#                           "none" or ""  - disables colorization
#                           "build-log"   - parses build logs
#
#   keepSHFBWarnings    - optionally strips out Sandcastle Help File Builder (SHFB) 
#                         warnings when identified
#
# REMARKS:
#
# NOTE: This function does nothing when the source file doesn't exist.

function Write-ActionOutputFile
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$groupTitle = $null,
        [Parameter(Position=2, Mandatory=$false)]
        [string]$type = $null,
        [Parameter(Position=3, Mandatory=$false)]
        [bool]$keepShfbWarnings = $false
    )

    if (![System.IO.File]::Exists($path))
    {
        return
    }

    $lines = [System.IO.File]::ReadAllLines($path)

    if (![System.String]::IsNullOrEmpty($groupTitle))
    {
        Open-ActionOutputGroup $groupTitle
    }

    # Build log error and warning regular expressions:

    $buildLogWarningRegex       = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "\(\d+,\d+.*\)\:\swarning\s"
    $buildLogErrorRegex         = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "\(\d+,\d+.*\)\:\serror\s"
    $buildLogWarningummaryRegex = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "^\s\s\s\s\d+[1-9] Warning\(s\)"
    $buildLogErrorSummaryRegex  = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "^\s\s\s\s\d+[1-9] Error\(s\)"
    $buildLogSHFBErrorRegex     = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "^\s*SHFB\s\:\serror"
    $buildLogSHFBWarningRegex   = New-Object "System.Text.RegularExpressions.Regex" -ArgumentList "^\s*SHFB\s\:\swarning"

    foreach ($line in $lines)
    {
        $color = $null

        switch ($type)
        {
            $null
            {
            }

            ""
            {
            }

            "none"
            {
            }

            "build-log"
            {
                if ($buildLogWarningRegex.IsMatch($line) -or $buildLogWarningummaryRegex.IsMatch($line))
                {
                    $color = "yellow"
                }
                elseif ($buildLogSHFBWarningRegex.IsMatch($line))
                {
                    if (!$keepSHFBWarnings)
                    {
                        return
                    }

                    $color = "yellow"
                }
                elseif ($buildLogErrorRegex.IsMatch($line) -or $buildLogErrorSummaryRegex.IsMatch($line) -or $buildLogSHFBErrorRegex.IsMatch($line))
                {
                    $color = "red"
                }
                elseif ($line.Contains("*** BUILD FAILED ***"))
                {
                    $color = "red"
                }
            }

            default
            {
                throw "[$type] is not a supported log file type."
            }
        }

        Write-ActionOutput $line $color
    }

    if (![System.String]::IsNullOrEmpty($groupTitle))
    {
        Close-ActionOutputGroup
    }
}

#------------------------------------------------------------------------------
# Sets a GitHub action environment variable (made available across the current
# job via the [env] collection).
#
# NOTE: The new environment value is not actually available in the action that
#       sets it but it will be available to all subsequent action executions.
#
# ARGUMENTS:
#
#   name        - the environment variable name
#   value       - the value to be set ($null will set empty string)

function Set-ActionEnvironmentVariable
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$value
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[name] cannot be null or empty."
    }

    if ($value -eq $null)
    {
        $value = ""
    }

    if ($value.Contains("\n"))
    {
        # Multiline values required special handling.

        $delimiter = "f06bca88-47d6-4971-b1dc-bec88fa4faac"

        [System.IO.File]::AppendAllText($env:GITHUB_ENV,  "$name<<$delimiter`r`n")
        [System.IO.File]::AppendAllText($env:GITHUB_ENV,  "$value")

        if (!$value.EndsWith("`n"))
        {
            [System.IO.File]::AppendAllText($env:GITHUB_ENV,  "`r`n")
        }

        [System.IO.File]::AppendAllText($env:GITHUB_ENV,  "$delimiter`r`n")
    }
    else
    {
        [System.IO.File]::AppendAllText($env:GITHUB_ENV, "$name=$value`r`n")
    }
}

#------------------------------------------------------------------------------
# Appends a directory path to the Action $GITHUB_PATH which will make any programs
# in the directory available for execution.
#
# NOTE: The new PATH entry value is not actually available in the action that
#       sets it but it will be available to all subsequent action executions.
#
# ARGUMENTS:
#
#   PATH    - the directory to be added to the path.

function Add-ActionPath
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path
    )

    if ([System.String]::IsNullOrEmpty($path))
    {
        throw "[path] cannot be null or empty."
    }

    if (![System.IO.Directory]::DirectoryExists($path))
    {
        throw "[$path] directory does not exist."
    }
}

#------------------------------------------------------------------------------
# Retrieves an action input value.
#
# ARGUMENTS:
#
#   name        - the value name.
#   required    - optionally indicates that the value is required
#   default     - optionally specifies the default value

function Get-ActionInput
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$false)]
        [bool]$required = $false,
        [Parameter(Position=2, Mandatory=$false)]
        [string]$default = $null
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[name] cannot be null or empty."
    }

    $name  = "INPUT_$name"
    $value = [System.Environment]::GetEnvironmentVariable($name)

    if ([System.String]::IsNullOrEmpty($value))
    {
        if (!$required)
        {
            return $default
        }
        else
        {
            throw "[$name] input is required."
        }
    }

    return $value
}

#------------------------------------------------------------------------------
# Retrieves an action input boolean value where "true" values return$ true and
# anything else returns $false.
#
# ARGUMENTS:
#
#   name        - the value name.
#   required    - optionally indicates that the value is required
#   default     - optionally specifies the default value

function Get-ActionInputBool
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$false)]
        [bool]$required = $false,
        [Parameter(Position=2, Mandatory=$false)]
        [bool]$default = $false
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[name] cannot be null or empty."
    }

    $name  = "INPUT_$name"
    $value = [System.Environment]::GetEnvironmentVariable($name)

    if ([System.String]::IsNullOrEmpty($value))
    {
        if (!$required)
        {
            return $default
        }
        else
        {
            throw "[$name] input is required."
        }
    }

    return $value -eq "true"
}

#------------------------------------------------------------------------------
# Retrieves an action input 32-bit integer value.
#
# ARGUMENTS:
#
#   name        - the value name.
#   required    - optionally indicates that the value is required
#   default     - optionally specifies the default value

function Get-ActionInputInt32
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$false)]
        [bool]$required = $false,
        [Parameter(Position=2, Mandatory=$false)]
        [int]$default = 0
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[name] cannot be null or empty."
    }

    $name  = "INPUT_$name"
    $value = [System.Environment]::GetEnvironmentVariable($name)

    if ([System.String]::IsNullOrEmpty($value))
    {
        if (!$required)
        {
            return $default
        }
        else
        {
            throw "[$name] input is required."
        }
    }

    return [System.Int32]::Parse($value)
}

#------------------------------------------------------------------------------
# Starts a GitHub workflow
#
# ARGUMENTS:
#
#   repo        - the repo path, like: [github.com/]nforgeio/neonCLOUD
#   workflow    - identifies the target workflow by name
#   branch      - optionbally specifies the branch or tag that holds the workflow
#   inputsJson  - optionally specifies any inputs as JSON formatted name/value pairs

function Invoke-ActionWorkflow
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$workflow,
        [Parameter(Mandatory=$false)]
        [string]$branch = "master",
        [Parameter(Mandatory=$false)]
        [string]$inputJson = $null
    )

    # Log into GitHub.

    Import-GitHubCredentials
    Login-GitHubUser $env:GITHUB_PAT

    try
    {
        # Start the target workflow.

        if ([System.String]::IsNullOrEmpty($inputJson))
        {
            Invoke-CaptureStreams "gh --repo $repo workflow run $workflow" | Out-Null
        }
        else
        {
            # We need to write the inputs to a temporary file so we can stream
            # them into the command via STDIN.

            $tempGuid      = [System.Guid]::NewGuid().ToString("d")
            $tempInputPath = [System.IO.Path]::Combine($env:TMP, "$tempGuid.inputs.json")

            [System.IO.File]::WriteAllText($tempInputPath, $inputJson)

            try
            {
                Invoke-CaptureStreams "gh --repo $repo workflow run $workflow --ref $branch --json < `"$tempInputPath`"" | Out-Null
            }
            finally
            {
                if ([System.IO.File]::Exists($tempInputPath))
                {
                    [System.IO.File]::Delete($tempInputPath)
                }
            }
        }
    }
    finally
    {
        Logout-GitHubUser
    }
}

#==============================================================================
# GitHub Release utilities                                                    #
#==============================================================================

#------------------------------------------------------------------------------
# Creates a new GitHub release.
#
# ARGUMENTS:
#
#   repo        - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   tagName     - Specifies the tag to be referenced by the release
#   releaseName - Optionally specifies the release name (defaults to [tagName])
#   body        - Optionally specifies the markdown formatted release notes
#   draft       - Optionally indicates that the release won't be published immediately
#   prerelease  - Optionally indicates that the release is not production ready
#   branch      - Optionally identifies the branch to be tagged.  This defaults to [master] 
#                 or [main] when either of those branches are already present
#
# RETURNS:
#
#   The [Octokit.Release] object.

function New-GitHubRelease
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$tagName,
        [Parameter(Mandatory=$false)]
        [string]$releaseName = $null,
        [Parameter(Mandatory=$false)]
        [string]$body = $null,
        [Parameter(Mandatory=$false)]
        [switch]$draft = $false,
        [Parameter(Mandatory=$false)]
        [switch]$prerelease = $false,
        [Parameter(Mandatory=$false)]
        [string]$branch = $null
    )

    return [Neon.Deployment.GitHub]::Release.Create($repo, $tagName, $releaseName, $body, $draft, $prerelease, $branch)
}

#------------------------------------------------------------------------------
# Updates a GitHub release.
#
# ARGUMENTS:
#
#   repo            - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   release         - Specifies the [Octokit.Release] being modified
#   releaseUpdate   - Specifies the [Octokit.ReleaseUpdate] with the revisions
#
# RETURNS:
#
#   The updated [Octokit.Release] object.

function Update-GitHubRelease
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [object]$release,
        [Parameter(Position=2, Mandatory=$true)]
        [object]$releaseUpdate
    )

    return [Neon.Deployment.GitHub]::Release.Update($repo, $release, $releaseUpdate)
}

#------------------------------------------------------------------------------
# Lists the releases for a GitHub repo.
#
# ARGUMENTS:
#
#   repo        - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#
# RETURNS:
#
#   A list of [Octokit.Release] instances.

function Get-GitHubReleases
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo
    )

    return [Neon.Deployment.GitHub]::Release.List($repo)
}

#------------------------------------------------------------------------------
# Obtains a specific for a GitHub repo release.
#
# ARGUMENTS:
#
#   repo        - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   tagName     - Identifies the release by its assocated tag
#
# RETURNS:
#
#   The requested [Octokit.Release] or $null when the release doesn't exist.

function Get-GitHubRelease
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$tagName
    )

    return [Neon.Deployment.GitHub]::Release.Get($repo, $tagName)
}

#------------------------------------------------------------------------------
# Uploads an asset file to a GitHub release.  Any existing asset with same
# name will be replaced.
#
# ARGUMENTS:
#
#   repo            - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   release         - Specifies the target release [Octokit:Release]
#   assetPath       - Path to the source asset file
#   assetName       - Optionally specifies the file name to assign to the asset.
#                     This defaults to the file name from [assetPath] 
#   contentType     - Optionally specifies the asset's Content-Type
#
# RETURNS:
#
#   The new [Octokit.ReleaseAsset] object.

function New-GitHubReleaseAssetFromFile
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [object]$release,
        [Parameter(Position=2, Mandatory=$true)]
        [string]$assetPath,
        [Parameter(Mandatory=$false)]
        [string]$assetName = $null,
        [Parameter(Mandatory=$false)]
        [string]$contentType = $null
    )

    return [Neon.Deployment.GitHub]::Release.UploadAsset($repo, $release, $assetPath, $assetName, $contentType)
}

#------------------------------------------------------------------------------
# Uploads asset data from a stream to a GitHub release.  Any existing asset 
# with same name will be replaced.
#
# ARGUMENTS:
#
#   repo            - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   release         - Specifies the target release [Octokit:Release]
#   assetStream     - The [System.IO.Stream] holding the asset data
#   assetName       - Optionally specifies the file name to assign to the asset.
#                     This defaults to the file name from [assetPath] 
#   contentType     - Optionally specifies the asset's Content-Type
#
# RETURNS:
#
#   The new [Octokit.ReleaseAsset] object.

function New-GitHubReleaseAssetFromStream
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [object]$release,
        [Parameter(Position=2, Mandatory=$true)]
        [object]$assetStream,
        [Parameter(Mandatory=$false)]
        [string]$assetName = $null,
        [Parameter(Mandatory=$false)]
        [string]$contentType = $null
    )

    return [Neon.Deployment.GitHub]::Release.UploadAsset($repo, $release, $assetStream, $assetName, $contentType)
}

#------------------------------------------------------------------------------
# Obtains the URI to be used to download an asset from a GitHub release.
#
# ARGUMENTS:
#
#   repo        - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   release     - Specifies the target release [Octokit:Release]
#   asset       - Identifies the target release asset [OctoKit.ReleaseAsset]
#
# RETURNS:
#
#   The asset's download URI.

function Get-GitHubReleaseAssetUri
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [object]$release,
        [Parameter(Position=2, Mandatory=$true)]
        [object]$asset
    )

    return [Neon.Deployment.GitHub]::Release.GetAssetUri($repo, $release, $asset)
}

#------------------------------------------------------------------------------
# Deletes a GitHub release.
#
# ARGUMENTS:
#
#   repo        - Identifies the target repo (like: "OWNER/REPO" or "github.com/OWNER/REPO")
#   release     - Specifies the target release [Octokit:Release]
#
# REMARKS:
#
#   This fails silently when the target release doesn't exist.

function Remove-GitHubRelease
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$repo,
        [Parameter(Position=1, Mandatory=$true)]
        [object]$release
    )

    return [Neon.Deployment.GitHub]::Release.Remove($repo, $release)
}
