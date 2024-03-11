#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         utility.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
        Start-Process pwsh.exe "-file",('"{0}"' -f $MyInvocation.MyCommand.Path) -Verb RunAs
        exit
    }
}

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
# Determines whether the current script is running as part of CI or other automation.
#
# RETURNS:
#
# $true when running under CI

function IsCI
{
    # This is set by GutHub runners.

    return $env:CI -eq "true"
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
# Writes a line of text as informational output.  This is output to STDOUT as
# hack so we can capture these lines via redirection.  We originally used 
# Write-Host for this which writes to the information stream #6 but we were
# unable to redirect stream #6 in Invoke-CaptureStreams via CMD.EXE so we're
# writing to STDOUT instead.
#
# ARGUMENTS:
#
#   message     - optionally specifies the line of text

function Write-Info
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$false)]
        [string]$message = $null
    )

    [Console]::WriteLine($message)
}

#------------------------------------------------------------------------------
# Writes information about a Powershell action exception to the output.
#
# ARGUMENTS:
#
#   err     - The error caught in a catch block via the automatic
#             [$_] or [$PSItem] variable

function Write-Exception
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        $err
    )

    $exception  = $err.Exception
    $message    = $exception.Message
    $info       = $err.InvocationInfo
    $scriptName = $info.ScriptName
    $scriptLine = $info.ScriptLineNumber

    Write-Info ""
    Write-Info "***************************************************************************"
    Write-Info "EXCEPTION:   $err"
    Write-Info "MESSAGE:     $message"
    Write-Info "SCRIPT NAME: $scriptName"
    Write-Info "SCRIPT LINE: $scriptLine"
    Write-Info "SCRIPT STACK TRACE"
    Write-Info "------------------"
    Write-Info $err.ScriptStackTrace

    if (![System.String]::IsNullOrEmpty($exception.StackTrace))
    {
        Write-Info ".NET STACK TRACE"
        Write-Info "----------------"
        Write-Info $exception.StackTrace
    }
    
    Write-Info "***************************************************************************"
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
# Executes a program, throwing an exception for non-zero exit codes by default.
#
# ARGUMENTS:
#
#   command     - program to run with any arguments
#   noCheck     - optionally disable non-zero exit code checks

function Invoke-Program
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
        throw "Empty command."
    }

    & cmd /c "$command"

    $exitCode = $LastExitCode

    if (!$noCheck -and $exitCode -ne 0)
    {
        throw "FAILED: $command`r`n[exitcode=$exitCode]"
    }
}

#------------------------------------------------------------------------------
# Executes a command and captures the stdout and/or stderr outputs.
#
# IMPORTANT: REDIRECTION BY COMMANDS IS NOT SUPPORTED!
#
# ARGUMENTS:
#
#   command     - program to run with any arguments
#   noCheck     - optionally disable non-zero exit code checks
#   interleave  - optionally combines the STDERR into STDOUT
#   noOutput    - optionally disables writing STDOUT and STDERR
#                 to the output
#
# RETURNS:
#
#   A three element hashtable with these properties:
#
#       exitcode    - the command's integer exit code
#       stdout      - the captured standard output
#       stderr      - the captured standard error
#       alltext     - combined standard input and output
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
        [switch]$interleave = $false,
        [Parameter(Mandatory=$false)]
        [switch]$noOutput = $false
    )

    if ([System.String]::IsNullOrEmpty($command))
    {
        throw "Empty command."
    }

    $guid       = [System.Guid]::NewGuid().ToString("d")
    $stdoutPath = [System.IO.Path]::Combine($env:TMP, "$guid.stdout")
    $stderrPath = [System.IO.Path]::Combine($env:TMP, "$guid.stderr")

    try
    {
        if (!$noOutput)
        {
            Write-Info
            Write-Info "RUN: $command"
            Write-Info
        }

        if ($interleave)
        {
            & cmd /c "$command > `"$stdoutPath`" 2>&1"
        }
        else
        {
            & cmd /c "$command > `"$stdoutPath`" 2> `"$stderrPath`""
        }

        $exitCode = $LastExitCode

        # Read the output files.

        $stdout = ""

        if ([System.IO.File]::Exists($stdoutPath))
        {
            $stdout = [System.IO.File]::ReadAllText($stdoutPath)
        }

        $stderr = ""

        if (!$interleave)
        {
            if ([System.IO.File]::Exists($stderrPath))
            {
                $stderr = [System.IO.File]::ReadAllText($stderrPath)
            }
        }

        $result          = @{}
        $result.exitcode = $exitCode
        $result.stdout   = $stdout
        $result.stderr   = $stderr
        $result.alltext  = "$stdout`r`n$stderr"

        if (!$noOutput)
        {
            if ($interleave)
            {
                Write-Info $result.stdout
            }
            else
            {
                Write-Info $result.alltext
            }
        }

        if (!$noCheck -and $exitCode -ne 0)
        {
            $exitcode = $result.exitcode
            $stdout   = $result.stdout
            $stderr   = $result.stderr

            throw "FAILED: $command`r`n[exitcode=$exitCode]`r`nSTDERR:`n$stderr`r`nSTDOUT:`r`n$stdout"
        }
    }
    finally
    {
        # Delete the temporary output files

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
        [System.IO.Directory]::CreateDirectory($folder) | Out-Null
    }

    $path = [System.IO.Path]::Combine($folder, "log.txt")

    [System.IO.File]::AppendAllText($path, $text + "`r`n")
}

#------------------------------------------------------------------------------
# Converts YAML text into a hashtable.
#
# ARGUMENTS:
#
#   yaml        - the YAML text
#
# RETURNS
#
#   The parsed hashtable.
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
# Converts a hashtable into YAML.
#
# ARGUMENTS:
#
#   table   - the hashtable
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
# Deletes a folder if it exists.

function DeleteFolder
{
    [CmdletBinding()]
    param (
		[Parameter(Position=0, Mandatory=$true)]
		[string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item -Recurse $Path | Out-Null
	} 
}

#------------------------------------------------------------------------------
# Pushes a Docker image to the public registry with retry as an attempt to handle
# transient registry issues.
#
# Note that you may set [$noImagePush=$true] to disable image pushing for debugging
# purposes.  The [publish.ps1] scripts accept the [--nopush] switchto control this.
#
# ARGUMENTS:
#
#		image		- The fully qualified image path including the version tag
#
#		baseTag		- Optionally specifies the original base image tag for
#					  image (used for tagging setupm images for cluster setup
#				      [debug mode]).
#
# REMARKS
#
# This function is used to publish packages to our container registries.  Base,
# service, and other non-setup image scripts will pass only the [image] argument.
#
# Setup image scripts will also pass [baseVersion] which will be set to the 
# original base version of the image.  This is required by cluster setup debug
# mode because that will need to pull the images from our public container
# registries directly.  When running in non-debug mode, cluster setup uses The
# packages already prepositioned in the node image and those were already tagged
# with the original base tag during node image creation.
#
# NOTE: This function attempts to workaround what appears to be transient issues.

$noImagePush = $false

function Push-ContainerImage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$image,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$baseTag = $null
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
			Write-Info "*** PUSH: RETRYING"
		}

		# $hack(jefflill):
		#
		# I'm seeing [docker push ...] with "blob upload unknown" messages in the
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

		$result   = Invoke-CaptureStreams "docker push $image" -interleave -noCheck
		$exitCode = $result.exitcode

        if ($exitcode -ne 0)
        {
		    if ($result.allText.Contains("blob upload unknown"))
		    {
			    Write-Info "*** PUSH: BLOB UPLOAD UNKNOWN"
			    $exitCode = 100
		    }
            else
            {
                Write-Info " "
                Write-Info "*******************************************************************************"
                Write-Info "ERROR:    [docker push $image] failed"
                Write-Info "EXITCODE: $exitcode"
                Write-Info "OUTPUT:"
                Write-Info "-------"
                Write-Info $result.stdout
                Write-Info "*******************************************************************************"
                Write-Info " "

                throw "[docker push $image] failed."
            }
        }

		if ($exitCode -eq 0)
		{																																		  
			# Add the base version tag if requested.  I don't believe it'll
			# be necessary to retry this operation.

			if (![System.String]::IsNullOrEmpty($baseTag))
			{
				# Strip the tag off the image passed.

				$fields    = $image -split ':'
				$baseImage = $fields[0] + ":" + $baseTag

				Write-Info "tag image: $image --> $baseImage"
				Invoke-CaptureStreams "docker tag $image $baseImage" -interleave | Out-Null
			}

			return
		}
		
		Write-Info "*** PUSH: EXITCODE=$exitCode"
		Start-Sleep 15
	}

	throw "[docker push $image] failed after [$maxAttempts] attempts."
}

#------------------------------------------------------------------------------
# Pulls a Docker image.
#
# ARGUMENTS:
#
#	imageRef	- the image reference
#
# REMARKS:
#
# NOTE: This function attempts to workaround what appears to be transient issues.

function Pull-ContainerImage
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$imageRef,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$baseTag = $null
    )

	$maxAttempts = 5

	for ($attempt=0; $attempt -lt $maxAttempts; $attempt++)
	{
		if ($attempt -gt 0)
		{
			Write-Info "*** PULL: RETRYING"
		}

		# $hack(jefflill):
		#
		# I'm seeing [docker pull ...] with "error pulling image configuration... EOF" 
		# messages in the output.  This appears to be transient because pulling again seems
		# to work.  I've seen some folks report this as being cause by networking issues.

		$result   = Invoke-CaptureStreams "docker pull $imageRef" -interleave -noCheck
		$exitCode = $result.exitcode

		if ($result.allText.Contains("error pulling image configuration"))
		{
			Write-Info "*** PULL: error pulling image configuration"
			$exitCode = 100
		}

		if ($exitCode -eq 0)
		{																																		  
			return
		}
		
		Write-Info "*** PULL: EXITCODE=$exitCode"
		Start-Sleep 15
	}

	throw "[docker pull $imageRef] failed after [$maxAttempts] attempts."
}

#------------------------------------------------------------------------------
# Checks to see of any Visual Studio instances are running and throws an exception
# when there are instances.  We see somewhat random build problems when Visual
# Studio has the solution open so we generally call this in build scripts to
# avoid problems.

function Ensure-VisualStudioNotRunning
{
    Get-Process -Name devenv -ErrorAction SilentlyContinue | Out-Null

    if ($?)
    {
        throw "ERROR: Please close all Visual Studio instances before building."
    }
}

#==============================================================================
# Location vs. Cwd (current working directory)
#
# The standard Set-Location, Push-Location, Pop-Location cmdlets don't really
# work like you'd expect for a normal scripting language.  Changing the current
# directory via these doesn't actually change the .NET (process) working directory:
# [Environment.CurrentDirectory].
#
# This is documented but is yet another unexpected Powershell quirk.  The
# rationale is that Powershell implements parallel processing via runspaces
# which seems to combine features of threads and processes.
#
# Our scripts are currently single threaded and since we're referencing .NET
# directly in a lot of our scripts (because we know .NET and the library classes
# seem to be a lot more predictable than the equivalent cmdlets), we're going to
# use the functions below to ensure that the current Powershell and .NET
# directories stay aligned.
#
# ==============================================================
# WARNING! THESE FUNCTIONS WILL NOT WORK WITH MULTIPLE RUNSPACES
# ==============================================================
#
# https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.management/pop-location?view=powershell-7.1

#------------------------------------------------------------------------------
# Returns the current directory, ensuring that the process directory matches the
# Powershell directory.
#
# RETURNS:
#
# The current directory.

function Get-Cwd
{
    $cwd                                   = Get-Location
    [System.Environment]::CurrentDirectory = $cwd

    return $cwd
}

#------------------------------------------------------------------------------
# Sets the current directory.
#
# ARGUMENTS:
#
#   path        - the new directory path

function Set-Cwd
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path
    )

    Set-Location $path
    [System.Environment]::CurrentDirectory = $path
}

#------------------------------------------------------------------------------
# Pushes the current directory onto an internal stack and sets a new current directory.
#
# ARGUMENTS:
#
#   path        - the new directory path

function Push-Cwd
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path
    )

    Push-Location $path | Out-Null
    [System.Environment]::CurrentDirectory = $path
}

#------------------------------------------------------------------------------
# Restores the current directory from an internal stack.

function Pop-Cwd
{
    Pop-Location | Out-Null
    [System.Environment]::CurrentDirectory = Get-Location
}

#------------------------------------------------------------------------------
# Returns a named string constant value from: $NK_ROOT\Lib\Neon.Kube\KubeVersion.cs

function Get-KubeVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$name
    )

    $version = $(& neon-build read-version "$env:NK_ROOT\Lib\Neon.Kube\KubeVersion.cs" $name)

    if ([System.String]::IsNullOrEmpty($version))
    {
        throw "Get-KubeVersion: [KubeVersion.$name] constant was not found."
    }

    return $version
}

#------------------------------------------------------------------------------
# Returns the path to the [helm-munger] tool in the NEONKUBE solution, throwing
# an exception tool binary does not exist.

function Get-HelmMungerPath
{
    $nkRoot = $env:NK_ROOT

    if ([String]::IsNullOrEmpty($nkRoot))
    {
        throw "[NK_ROOT] environment variable does not exist.  NEONKUBE git repo must be cloned locally."
    }

    $helmMungerPath = [System.IO.Path]::Combine($nkRoot, "Tools", "helm-munger", "bin", "Debug", "net8.0", "win-x64", "helm-munger.exe")

    if (-not [System.IO.File]::Exists($helmMungerPath))
    {
        throw "Cannot locate the [helm-munger] tool at [$helmMungerPath].  Rebuild it and try again."
    }

    return $helmMungerPath
}

#------------------------------------------------------------------------------
# Executes the [helm-munger dependency remove-repositories CHART-FOLDER] command
# to recursively remove [dependency.repository] properties from [v2] [Chart.yaml]
# files.

function Remove-HelmRepositories
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$chartFolder
    )

    $helmMungerPath = Get-HelmMungerPath
    Invoke-Program "`"$helmMungerPath`" dependency remove-repositories `"$chartFolder`""
}

#------------------------------------------------------------------------------
# Executes the [helm-munger dependency dependency remove CHART-FOLDER DEPENDENCY]
# command to remove a specific chart dependency.

function Remove-HelmDependency
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$chartFolder,
        [Parameter(Position=1, Mandatory=$true)]
        [string]$dependencyName
    )

    $helmMungerPath = Get-HelmMungerPath
    Invoke-Program "`"$helmMungerPath`" dependency remove `"$chartFolder`" $dependencyName"
}
