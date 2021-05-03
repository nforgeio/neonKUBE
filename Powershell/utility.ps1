#Requires -Version 7.0
#------------------------------------------------------------------------------
# FILE:         utility.ps1
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
# Requests that the user elevate the script permission if the current process
# isn't already running with elevated permissions.

function Request-AdminPermissions
{
    if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
    {
        # Relaunch as an elevated process:
        Start-Process powershell.exe "-file",('"{0}"' -f $MyInvocation.MyCommand.Path) -Verb RunAs
        exit
    }
}

#------------------------------------------------------------------------------
# Escapes any double quote characters in a string by replacing quotes with two
# double quotes.
#
# ARGUMENTS:
#
#   text        - the input string

function EscapeDoubleQuotes
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$text
    )

    if ([System.String]::IsNullOrEmpty($text))
    {
        return $text
    }

    return $text.Replace("`"", "`"`"")
}

#------------------------------------------------------------------------------
# Executes a command and captures the stdout and/or stderr outputs.
#
# IMPORTANT: REDIRECTION IN COMMANDS IS NOT SUPPORTED
#
# ARGUMENTS:
#
#   command     - program to run with any arguments
#   noCheck     - optionally disable non-zero exit code checks
#
# RETURNS:
#
#   A three element hash table with these properties:
#
#       exitcode    - the command's integer exit code
#       stdout      - the captured standard output
#       stderr      - the captured standard error
#
# REMARKS:
#
# NOTE: This function checks for non-zero exit codes by default.
#
# It's insane that Powershell doesn't have this capability built-in.  
# It looks likes Invoke-Expression may have implemented this in the past,
# but recent versions seem to have deprecated this.  stdout can be captured
# easily from command executions but not stderr.  Powershell seems to handle
# stderr specially and it returns as a PSObject rather a string and I 
# couldn't find what looked like a reasonable way to convert that into agreed
# string.
#
# I did run across a MSFT proposal to make this possible, but they're still
# debating the details.  This seems like such a basic scripting feature that
# I'm amazed that this senerio isn't covered cleanly.
#
# The solution here is to run the command in CMD.EXE for now (and perhaps
# Bash for eveything else) while piping stdout/stderr to temporary files
# and then read and delete those files so the streams can be returned as
# variables.

function Invoke-CaptureStreams
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$command,
        [Parameter(Mandatory=$false)]
        [switch]$noCheck = $false
    )

    if ([System.String]::IsNullOrEmpty($command))
    {
        throw "Invalid command."
    }

    $guid       = [System.Guid]::NewGuid().ToString("d")
    $stdoutPath = [System.IO.Path]::Combine($env:TMP, "$guid.stdout")
    $stderrPath = [System.IO.Path]::Combine($env:TMP, "$guid.stderr")

    try
    {
        & cmd /c "$command > `"$stdoutPath`" 2> `"$stderrPath`""
        $exitCode = $LastExitCode

        # Read the output files.

        $stdout = ""

        if ([System.IO.File]::Exists($stdoutPath))
        {
            $stdout = [System.IO.File]::ReadAllText($stdoutPath)
        }

        $stderr = ""

        if ([System.IO.File]::Exists($stderrPath))
        {
            $stderr = [System.IO.File]::ReadAllText($stderrPath)
        }

        $result          = @{}
        $result.exitcode = $exitCode
        $result.stdout   = $stdout
        $result.stderr   = $stderr

        if (!$noCheck -and $exitCode -ne 0)
        {
            $exitcode = $result.exitcode
            $stdout   = $result.stdout
            $stderr   = $result.stderr

            throw "Invoke-CaptureStreams Failed: [exitcode=$exitCode]`nSTDERR:`n$stderr`nSTDOUT:`n$stdout"
        }
    }
    finally
    {
        if ([System.IO.File]::Exists($stdoutPath))
        {
            [System.IO.File]::Delete($stdoutPath)
        }

        if ([System.IO.File]::Exists($stderrPath))
        {
            [System.IO.File]::Delete($stderrPath)
        }
    }

    return $result
}

#------------------------------------------------------------------------------
# Splits a multi-line string into an array of strings, one for each line.
#
# ARGUMENTS:
#
#   text    - the string to be split
#
# RETURNS
#
# The line array.
#
# REMARKS:
#
# NOTE: A one element array with an empty string will be returned for 
#       $null or empty inputs.

function ToLineArray
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$text
    )

    if ([System.String]::IsNullOrEmpty($text))
    {
        return @( "" )
    }

    # Remove any carriage returns and then split on new lines.

    $text = $text.Replace("`r", "")

    return $text.Split("`n")
}
