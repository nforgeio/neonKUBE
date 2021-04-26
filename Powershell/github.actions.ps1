#------------------------------------------------------------------------------
# GitHub Action utility functions
#
# These functions integrate with a GutHub jobrunner for things like setting
# action outputs or environment variables.  This works by writing specially
# formatted command lines to STDOUT which the runner will parse and execute.
#
#   https://docs.github.com/en/actions/reference/workflow-commands-for-github-actions

# WARNING!
#
# This file is present in multiple neonFORGE repositories and any changes should
# be replicated to all repos.  We'll consider neonCLOUD to be the authoritative 
# repo and you'll need to take care to manually upgrade the others.
#
#       nforgeio/neoCLOUD   $/Powershell/github.actions.ps1 * AUTHORITATIVE
#       nforgeio/neonKUBE   $/Powershell/github.actions.ps1

#------------------------------------------------------------------------------
# Escapes a potentially multi-line string such that it can be written to STDOUT
# and be processed correctly by the jobrunner.
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
#   text    - the string being written or $null to write an empty line.
#   color   - optionally specifies the text color (one of: 'red' or 'yellow')

function Write-ActionOutput
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$false)]
        [string]$text = $null,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$color = $null
    )

    if ($text -eq $null)
    {
        $text = ""
    }

    $text = Escape-ActionString $text

    if (![System.String]::IsNullOrEmpty($text))
    {
        Switch ($color)
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

    [System.Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    [System.Console]::WriteLine($text)
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

function Write-ActionError
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$message
    )

    Write-ActionOutput $message "red"
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

    Write-ActionError "EXCEPTION: $error"
    Write-ActionError "-------------------------------------------"
    Write-ActionError $error.ScriptStackTrace
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

    ForEach ($line in $lines)
    {
        $color = $null

        Switch ($type)
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

function Get-ActionInput
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name,
        [Parameter(Position=1, Mandatory=$false)]
        [bool]$required = $false
    )

    if ([System.String]::IsNullOrEmpty($name))
    {
        throw "[name] cannot be null or empty."
    }

    $name  = "INPUT_$name"
    $value = [System.Environment]::GetEnvironmentVariable($name)

    if ($required -and [System.String]::IsNullOrEmpty($value))
    {
        throw "[$name] input is required."
    }

    return $value
}
