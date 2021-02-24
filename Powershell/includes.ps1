#------------------------------------------------------------------------------
# FILE:         includes.ps1
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

$ErrorActionPreference = "Stop"

#------------------------------------------------------------------------------
# Common error handling

$ErrorActionPreference = "Stop"
$PSDefaultParameterValues['*:ErrorAction']='Stop'

# Call this after every native command to check for non-zero exit codes.
function ThrowOnExitCode {

    if ($LastExitCode -ne 0)
    {
        throw "ERROR: exitcode=$LastExitCode"
    }
}

#------------------------------------------------------------------------------
# Returns the current branch for a git repostory.

function GitBranch
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$gitRepoPath
    )

    Push-Location
    Set-Location $gitRepoPath

    $branch = git rev-parse --abbrev-ref HEAD

    Pop-Location

    return $branch
}

#------------------------------------------------------------------------------
# Signs into into 1Password so you'll be able to access secrets.  This will start
# a 30 minute session that will allow you to use the 1Password [op] CLI without
# requiring additional credentials.  We recommend that you call this at the 
# beginning of all CI/CD operations and then immediately grab all of the secrets 
# you'll need for that operation.  This way you won't need to worry about the
# 30 minute session expiring.
#
# This script requires the following environment variables to be defined:
#
#   NEON_OP_DOMAIN
#   NEON_OP_EMAIL_ADDRESS
#   NEON_OP_SECRET_KEY
#   NEON_OP_SIGNIN_ADDRESS
#   NEON_OP_MASTER_PASSWORD     * Optional
#
# NEON_OP_MASTER_PASSWORD is optional and you'll be prompted for this (once for 
# the current Powershell session) if this isn't defined.  The other variables
# are initialized by [$/buildenv.cmd] so re-run that as administrator if necessary.

function OpSignin
{
    $errorHelp = " environment variable is not defined.  Please run [$/buildenv.cmd] as administrator."

    if ($env:NEON_OP_DOMAIN -eq "")
    {
        "[NEON_OP_DOMAIN]" + $errorHelp
        exit 1
    }

    if ($env:NEON_OP_EMAIL_ADDRESS -eq "")
    {
        "[NEON_OP_EMAIL_ADDRESS]" + $errorHelp
        exit 1
    }

    if ($env:NEON_OP_SECRET_KEY -eq "")
    {
        "[NEON_OP_SECRET_KEY]" + $errorHelp
        exit 1
    }

    if ($env:NEON_OP_SIGNIN_ADDRESS -eq "")
    {
        "[NEON_OP_EMAIL_ADDRESS]" + $errorHelp
        exit 1
    }

    if ($env:NEON_OP_SESSION_TOKEN -eq "" -or $env:NEON_OP_SESSION_TOKEN -eq $null)
    {
        ""
        $env:NEON_OP_SESSION_TOKEN = $(& op signin --raw $env:NC_OP_DOMAIN)
        ""
    }
}

#------------------------------------------------------------------------------
# Handles returning the user's default vault when one isn't specified.
#
# Usage: OpGetVault NAME [VAULT]
#
# ARGUMENTS:
#
#       NAME        - Specifies the password item name in 1Password
#       VAULT       - Optionally specifies the 1Password vault holding
#                     the item.  This defaults to the user's defaults
#                     vault.

function OpGetVault
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [AllowEmptyString()]
        [string]$vault
    )

    if (($vault -eq $null) -or ($vault -eq ""))
    {
        $vault = $env:NC_USER

        if (($vault -eq $null) -or ($vault -eq ""))
        {
            ""
            "*** ERROR: NC_USER environment variable doesn't exist.  You may need to re-run [buildenv.cmd] as administrator."
            ""
            exit 1
        }

        $vault = "user-" + $vault
    }

    return $vault
}

#------------------------------------------------------------------------------
# Retrieves [password] field from from a password in the user's 1Password account.  
# This will be written to STDOUT as plaintext.
#
# Usage: OpGetPassword NAME [VAULT]
#
# ARGUMENTS:
#
#       NAME        - Specifies the password item name in 1Password
#       VAULT       - Optionally specifies the 1Password vault holding
#                     the item.  This defaults to the user's defaults
#                     vault.
#
# Each user will be provisioned with a default vault named [user-USERNAME]
# where USERNAME is the from the user's primary neonFORGE Office email,
# as in [sally] from [sally@neonforge.com].  USERNAME is persisted as the
# NC_USER environment variable by the neonCLOUD [build.env.cmd] script. 
# Note that this variable is required when no vault is specified.

function OpGetPassword
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$passwordName,
        [Parameter(Position=1, Mandatory=0)]
        [string]$vault = $null
    )

    $vault = OpGetVault $vault

    op --session $env:NEON_OP_SESSION_TOKEN get item $passwordName --vault $vault --fields password
}


#------------------------------------------------------------------------------
# Retrieves the [value] field from from the user's 1Password account.  This will
# be written to STDOUT as plaintext.
#
# Usage: OpGetValue NAME [VAULT]
#
# ARGUMENTS:
#
#       NAME        - Specifies the password item name in 1Password
#       VAULT       - Optionally specifies the 1Password vault holding
#                     the item.  This defaults to the user's defaults
#                     vault.
#
# Each user will be provisioned with a default vault named [user-USERNAME]
# where USERNAME is the from the user's primary neonFORGE Office email,
# as in [sally] from [sally@neonforge.com].  USERNAME is persisted as the
# NC_USER environment variable by the neonCLOUD [build.env.cmd] script. 
# Note that this variable is required when no vault is specified.

function OpGetValue
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$passwordName,
        [Parameter(Position=1, Mandatory=0)]
        [string]$vault = $null
    )

    $vault = OpGetVault $vault

    op --session $env:NEON_OP_SESSION_TOKEN get item $passwordName --vault $vault --fields value
}
