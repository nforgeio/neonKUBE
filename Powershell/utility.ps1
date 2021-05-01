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
# Executes a command and optionally captures the stdout and/or stderr outputs
# as variables as well as returning the exit code as the function result.
#
# IMPORTANT: REDIRECTION IN COMMANDS IS NOT SUPPORTED
#
# ARGUMENTS:
#
#   command         - program to run with any arguments
#   stdoutVariable  - optional name of the variable to return the stdout
#   stderrVariable  - optional name of the variable to return the stderr
#   checkExitCode   - optionally throw and error for non-zero exit codes
#
# REMARKS:
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

function ExecuteCapture
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$command,
        [Parameter(Mandatory=$false)]
        [string]$stdoutVariable = $null,
        [Parameter(Mandatory=$false)]
        [string]$stderrVariable = $null,
        [Parameter(Mandatory=$false)]
        [switch]$checkExitCode = $false
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
        & cmd /c $command > $stdoutPath 2> $stderrPath
        $exitCode = $LastExitCode

        if ($checkExitCode -and $exitCode -ne 0)
        {
            throw "ERROR: exitcode=$exitCode"
        }

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

        # Set the variables as requested.

        if (![System.String]::IsNullOrEmpty($stdoutVariable))
        {
            Set-Variable -Name $stdoutVariable -Value $stdout -Scope global
        }

        if (![System.String]::IsNullOrEmpty($stderrVariable))
        {
            Set-Variable -Name $stderrVariable -Value $stderr -Scope global
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

    return $exitCode
}

