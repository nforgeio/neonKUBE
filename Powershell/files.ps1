#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         files.ps1
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

#------------------------------------------------------------------------------
# Recuresively removes the contents of a filesystem directory if it exists.
#
# ARGUMENTS:
#
#   path            - path to the directory being cleared
#   ignoreErrors    - optionally ignore any errors
#
# REMARKS:
#
# Pass [ignoreErrors=$true] when there's a possibility some of the files
# or subdirectories may be locked or restricted but the operation should
# continue and delete what it can.

function Clear-Directory
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path,
        [Parameter(Position=1, Mandatory=$false)]
        [switch]$ignoreErrors = $false
    )

    if ($ignoreErrors)
    {
        if ([System.String]::IsNullOrEmpty($path) -or ![System.IO.Directory]::Exists($path))
        {
            return
        }

        foreach ($filePath in [System.IO.Directory]::GetFiles($path))
        {
            try
            {
                [System.IO.File]::Delete($filePath)
            }
            catch
            {
                # Ignoring this
            }
        }

        foreach ($folderPath in [System.IO.Directory]::GetDirectories($path))
        {
            try
            {
                [System.IO.Directory]::Delete($folderPath, $true)
            }
            catch
            {
                # Ignoring this
            }
        }
    }
    else
    {
        if ([System.String]::IsNullOrEmpty($path) -or ![System.IO.Directory]::Exists($path))
        {
            return
        }

        foreach ($filePath in [System.IO.Directory]::GetFiles($path))
        {
            [System.IO.File]::Delete($filePath)
        }

        foreach ($folderPath in [System.IO.Directory]::GetDirectories($path))
        {
            [System.IO.Directory]::Delete($folderPath, $true)
        }
    }
}

#------------------------------------------------------------------------------
# Deletes a file if it exists.

function Delete-File
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$Path
    )

    # Delete the temporary output files

    try
    {
        if ([System.IO.File]::Exists($Path))
        {
            [System.IO.File]::Delete($Path)
        }
    }
    catch
    {
        # We've seen some files remaining open by another process
        # for a brief period of time while the process is terminating.
        # We'll wait a bit and then try again once.

        [System.Threading.Thread]::Sleep(1000)
        [System.IO.File]::Delete($Path)
    }
}

#------------------------------------------------------------------------------
# Deletes a folder if it exists.

function Delete-Folder
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
