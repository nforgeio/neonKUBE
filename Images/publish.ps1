#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
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

# Builds and publishes all of the Neon Docker images.
#
# NOTE: You must be already logged into the target container registry.
#
# USAGE: pwsh -f publish-all.ps1 [-all]

param 
(
    [Parameter(Position=0, Mandatory=$false)]
    [string]$config,                        # Identifies the build configuration
    [switch]$all             = $false,      # Rebuild all images
    [switch]$base            = $false,      # Rebuild base images
    [switch]$test            = $false,      # Rebuild test related images
    [switch]$other           = $false,      # Rebuild all other images (usually script based)
    [switch]$services        = $false,      # Rebuild all cluster service images
    [switch]$nopush          = $false,      # Don't push to the registry
    [switch]$noprune         = $false,      # Don't prune the local Docker cache
    [switch]$noclean         = $false,      # Don't clean before building
    [switch]$allVersions     = $false,      # Rebuild all image versions
    [switch]$rethrow         = $false,      # Rethrow any exceptions (used when called by other scripts)
    [switch]$nobuildsolution = $false       # Don't clean or build the solution
)

#----------------------------------------------------------
# Global includes
$image_root = [System.IO.Path]::Combine($env:NK_ROOT, "Images")
. $image_root/includes.ps1
#----------------------------------------------------------

#------------------------------------------------------------------------------
# Builds and publishes a container image, passing $config.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$Path
    )

    Push-Cwd "$Path" | Out-Null

    try
    {
        if ($allVersions)
        {
            if ($nopush)
            {
                Invoke-Program "pwsh -NonInteractive -f ./publish.ps1 -config $config -all -nopush"
            }
            else
            {
                Invoke-Program "pwsh -NonInteractive -f ./publish.ps1 -config $config -all"
            }
        }
        else
        {
            if ($nopush)
            {
                Invoke-Program "pwsh -NonInteractive -f ./publish.ps1 -config $config -nopush"
            }
            else
            {
                Invoke-Program "pwsh -NonInteractive -f ./publish.ps1 -config $config"
            }
        }
    }
    finally
    {
        Pop-Cwd | Out-Null
    }
}

#------------------------------------------------------------------------------
# Builds and publishes a container image, WITHOUT passing $config.

function PublishWithoutConfig
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=$true)]
        [string]$path,
        [Parameter(Position=1, Mandatory=$false)]
        [string]$noBuildOption = $null
    )

    try
    {
        Push-Cwd "$path" | Out-Null

        $noPushOption = ""
    
        if ($nopush)
        {
            $noPushOption = "-nopush"
        }

        Invoke-Program "pwsh -NonInteractive -f publish.ps1 $noPushOption $noBuildOption"
    }
    finally
    {
        Pop-Cwd | Out-Null
    }
}

#------------------------------------------------------------------------------
# Main

try
{
    #--------------------------------------------------------------------------
    # Process the command line arguments.

    if ([System.String]::IsNullOrEmpty($config))
    {
        $config = "Debug"
    }

    if ($all)
    {
        $base     = $true
        $test     = $true
        $other    = $true
        $services = $true
    }
    elseif ((-not $base) -and (-not $test) -and (-not $other) -and (-not $services))
    {
        # Build everything but base images by default.

        $base     = $false
        $test     = $true
        $other    = $true
        $services = $true
    }

    #------------------------------------------------------------------------------
    # Verify that the user has the required environment variables.  These will
    # be available only for maintainers and are intialized by the NEONCLOUD
    # [buildenv.cmd] script.

    if (-not (Test-Path env:NC_ROOT))
    {
        "*** ERROR: This script is intended for use by maintainers only:"
        "           [NC_ROOT] environment variable is not defined."
        ""
        "           Maintainers should re-run the NEONCLOUD [buildenv.cmd] script."

        return 1
    }

    #------------------------------------------------------------------------------
    # We need to do a solution build to ensure that any tools or other dependencies 
    # are built before we build and publish the individual container images.

    if ([System.String]::IsNullOrEmpty($env:SolutionName))
    {
        $env:SolutionName = "neonKUBE"
    }

    $msbuild    = $env:MSBUILDPATH
    $neonBuild  = "$env:NF_ROOT\ToolBin\neon-build\neon-build.exe"
    $nkRoot     = "$env:NK_ROOT"
    $nkSolution = "$nkRoot\neonKUBE.sln"
    $branch     = GitBranch $nkRoot

    if (-not $nobuildsolution)
    {
        if (-not $noclean)
        {
            Write-Info ""
            Write-Info "********************************************************************************"
            Write-Info "***                            CLEAN SOLUTION                                ***"
            Write-Info "********************************************************************************"
            Write-Info ""

            Invoke-Program "`"$neonBuild`" clean `"$nkRoot`""
        }

        Write-Info  ""
        Write-Info  "*******************************************************************************"
        Write-Info  "***                           BUILD SOLUTION                                ***"
        Write-Info  "*******************************************************************************"
        Write-Info  ""

        & "$msbuild" "$nkSolution" -p:Configuration=$config -t:restore,build -p:RestorePackagesConfig=true -m -verbosity:quiet

        if (-not $?)
        {
            throw "ERROR: BUILD FAILED"
        }
    }

    #------------------------------------------------------------------------------
    # Build the container images.

    # Purge any local Docker images as well as the image build cache.
    # This also purges all other Docker assets as a side effect.  We
    # need to do this to ensure to ensure a clean build.

    if (-not $noprune)
    {
        Invoke-CaptureStreams "docker system prune -af" -interleave | Out-Null
    }

    # NOTE: 
    #
    # The build order below is important since later images
    # may depend on earlier ones.

    if ($base)
    {
        # Base images: it's lonely here!
    }

    if ($services)
    {
        Publish "$image_root\neon-acme"
        Publish "$image_root\neon-cluster-operator"
        Publish "$image_root\neon-node-agent"
        Publish "$image_root\neon-sso-session-proxy"
    }

    # Purge any local Docker images as well as the image build cache.
    # This also purges all other Docker assets as a side effect.
    #
    # We're doing this to ensure that Docker is reset to its default
    # state after building images.  This is especially important for
    # GitHub runners.

    if (-not $noprune)
    {
        Invoke-CaptureStreams "docker system prune -af" -interleave | Out-Null
    }
}
catch
{
    Write-Exception $_

    if ($rethrow)
    {
        throw
    }

    exit 1
}
