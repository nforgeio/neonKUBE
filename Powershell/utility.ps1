#Requires -Version 7.0 -RunAsAdministrator
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
# Loads an assembly file into the current appdomain, if it's not already loaded.
#
# ARGUMENTS:
#
#   path        - path to the assembly FILE

function Load-Assembly
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path
    )

    try
    {
        $path = [System.IO.Path]::GetFullPath($path)

        # We're going to maintain a list of the assembly files that we've
        # loaded in a global variable so we can avoid loading the same
        # assembly multiple times.

        if ($null -eq $global:__LOADED_ASSEMBLIES)
        {
            $global:__LOADED_ASSEMBLIES = @{}
        }

        if ($global:__LOADED_ASSEMBLIES.ContainsKey($path))
        {
            return;     # Assembly is already loaded.
        }

        $global:__LOADED_ASSEMBLIES[$path] = "loaded"

        Add-Type -Path $path
    }
    catch
    {
        Write-Exception $_
        throw
    }
}

#------------------------------------------------------------------------------
# Writes information about a Powershell action exception to the output.
#
# ARGUMENTS:
#
#   error   - The error caught in a catch block via the automatic
#             [$_] or [$PSItem] variable

function Write-Exception
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        $error
    )

    Write-Stdout "EXCEPTION: $error"
    Write-Stdout "-------------------------------------------"
    Write-Stdout $($_.Exception | Format-List -force)
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
# Writes a line of text to STDOUT to avoid any of Powershell's pipeline semantics.
# This is useful for situations where commands fail but nothing gets written to
# a redirected log file.
#
# ARGUMENTS:
#
#   text        - optionally specifies the line of text to be written

function Write-Stdout
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$text
    )

    # $debug(jefflill)

    # [Systen.Console]::WriteLine($text)

    Log-DebugLine $text
}

#------------------------------------------------------------------------------
# Writes a line of text to STDERR to avoid any of Powershell's pipeline semantics.
# This is useful for situations where commands fail but nothing gets written to
# a redirected log file.
#
# ARGUMENTS:
#
#   text        - optionally specifies the line of text to be written

function Write-Stderr
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$text
    )

    # $debug(jefflill)

    # [Systen.Console.Error]::WriteLine($text)

    Log-DebugLine $text
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
#   interleave  - optionally combines the STDERR into STDOUT
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
        [switch]$noCheck = $false,
        [Parameter(Mandatory=$false)]
        [switch]$interleave = $false
    )

Log-DebugLine "InvokeCapture-0:"
    if ([System.String]::IsNullOrEmpty($command))
    {
        throw "Invalid command."
    }
Log-DebugLine "InvokeCapture-1:"

    $guid       = [System.Guid]::NewGuid().ToString("d")
    $stdoutPath = [System.IO.Path]::Combine($env:TMP, "$guid.stdout")
    $stderrPath = [System.IO.Path]::Combine($env:TMP, "$guid.stderr")

Log-DebugLine "InvokeCapture-2:"
try
    {
        if ($interleave)
        {
            & cmd /c "$command > `"$stdoutPath`" 2>&1"
        }
        else
        {
            & cmd /c "$command > `"$stdoutPath`" 2> `"$stderrPath`""
        }

        $exitCode = $LastExitCode
Log-DebugLine "InvokeCapture-3:"

        # Read the output files.

        $stdout = ""

        if ([System.IO.File]::Exists($stdoutPath))
        {
            $stdout = [System.IO.File]::ReadAllText($stdoutPath)
        }
Log-DebugLine "InvokeCapture-4:"

        $stderr = ""

        if (!$interleave)
        {
            if ([System.IO.File]::Exists($stderrPath))
            {
                $stderr = [System.IO.File]::ReadAllText($stderrPath)
            }
        }
Log-DebugLine "InvokeCapture-5:"

        $result          = @{}
        $result.exitcode = $exitCode
        $result.stdout   = $stdout
        $result.stderr   = $stderr
Log-DebugLine "InvokeCapture-6:"

        Write-Stdout "COMMAND: $command"
        Write-Stdout "EXITCODE: $result.exitcode"
        Write-Stdout "STDOUT:"
        Write-Stdout "-------------------------------------------"
        Write-Stdout $result.stdout
        Write-Stdout "-------------------------------------------"
        Write-Stdout "STDERR:"
        Write-Stdout "-------------------------------------------"
        Write-Stdout $result.stderr
        Write-Stdout "-------------------------------------------"
Log-DebugLine "InvokeCapture-6A:"

        if (!$noCheck -and $exitCode -ne 0)
        {
Log-DebugLine "InvokeCapture-7:"
            $exitcode = $result.exitcode
            $stdout   = $result.stdout
            $stderr   = $result.stderr

            throw "Invoke-CaptureStreams Failed: [exitcode=$exitCode]`r`nSTDERR:`n$stderr`r`nSTDOUT:`r`n$stdout"
        }
    }
    finally
    {
Log-DebugLine "InvokeCapture-8:"
        if ([System.IO.File]::Exists($stdoutPath))
        {
            [System.IO.File]::Delete($stdoutPath)
        }
Log-DebugLine "InvokeCapture-9:"

        if ([System.IO.File]::Exists($stderrPath))
        {
            [System.IO.File]::Delete($stderrPath)
        }
Log-DebugLine "InvokeCapture-10:"
    }
Log-DebugLine "InvokeCapture-11:"

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

#------------------------------------------------------------------------------
# Appends a line of text to [C:\Temp\log.txt] as a very simple logging mechanism
# to be used while debugging Powershell scripts, specifically GitHub Actions.
#
# ARGUMENTS:
#
#   text        - the text to be appended

function Log-DebugLine
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [AllowNull()]
        [AllowEmptyString()]
        [string]$text
    )

    $folder = "C:\Temp"

    if (![System.IO.Directory]::Exists($folder))
    {
        [System.IO.Directory]::CreateDirectory($folder)
    }

    $path = [System.IO.Path]::Combine($folder, "log.txt")

    [System.IO.File]::AppendAllText($path, $text + "`r`n")
}

#------------------------------------------------------------------------------
# Converts YAML text into a hash table.
#
# ARGUMENTS:
#
#   yaml        - the YAML text
#
# RETURNS
#
#   The parsed hash table.
#
# REMARKS:
#
# WRNING! This is currently just hacked together and supports only one nesting
# level of object properties.  Arrays are not supported either.

function ConvertFrom-Yaml
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$yaml
    )

    Load-Assembly "$env:NEON_ASSISTANT_HOME\YamlDotNet.dll"

    $deserializer = $(New-Object "YamlDotNet.Serialization.DeserializerBuilder").Build()
    $reader       = New-Object "System.IO.StringReader" -ArgumentList $yaml 
    
    return $deserializer.Deserialize($reader)
}

#------------------------------------------------------------------------------
# Converts a hash table into YAML.
#
# ARGUMENTS:
#
#   table   - the hash table
#
# RETURNS
#
#   The YAML text.
#
# REMARKS:
#
# WARNING! This has never been tested.

function ConvertTo-Yaml
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$table
    )

    Load-Assembly "$env:NEON_ASSISTANT_HOME\YamlDotNet.dll"

    $serializer = $(New-Object "YamlDotNet.Serialization.SerializerBuilder").Build()

    return $serializer.Serialize($table)
}

#------------------------------------------------------------------------------
# Logs into Docker using the named credentials from the current user's 1Password
# user folder.
#
# ARGUMENTS:
#
#   server              - the server endpoint, typically one of:
#
#       ghcr.io
#       docker.io
#
#   loginCredentials    - Identifies the 1Password login to use, typically one of:
#
#       GITHUB_PAT   * recommended for GitHub package operations
#       GITHUB_LOGIN
#       DOCKER_LOGIN

function Login-Docker
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$server,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$credentials
    )

    $username = $(Get-SecretValue "$credentials[username]")
    $password = $(Get-SecretValue "$credentials[password]")
    
    Write-Output $password | docker login $server -u $username --password-stdin

    $exitCode = $LastExitCode

    if ($exitCode -ne 0)
    {
        throw "Docker login failed: server=[$server] username=[$username]"
    }
}

#------------------------------------------------------------------------------
# Logs out of Docker, optionally logging out of a specific server.
#
# ARGUMENTS:
#
#   server      - optionally specifies the server to log out from, typically
#                 one of:
#
#                       docker.io
#                       ghcr.io
#
#   CIOnly      - optionally logs out only when the current script is running
#                 a CI job.  This is nice to avoit logging develepers out on
#                 their own workstations when runing local CI tests.

function Logout-Docker
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$false)]
        [string]$server = $null,
        [Parameter(Position=1, Mandatory=$false)]
        [switch]$CIOnly = $false
    )

    if ($CIOnly -and $env:CI -eq "true")
    {
        return;
    }

    if (![System.String]::IsNullOrEmpty($server))
    {
        docker logout $server
    }
    else
    {
        docker logout
    }

    # $hack(jefflill): Do we care about checking the exit code here?
}
