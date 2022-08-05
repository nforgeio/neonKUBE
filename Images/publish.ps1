#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    [switch]$all         = $false,      # Rebuild all images
    [switch]$base        = $false,      # Rebuild base images
    [switch]$test        = $false,      # Rebuild test related images
    [switch]$other       = $false,      # Rebuild all other images (usually script based)
    [switch]$services    = $false,      # Rebuild all cluster service images
    [switch]$nopush      = $false,      # Don't push to the registry
    [switch]$noprune     = $false,      # Don't prune the local Docker cache
    [switch]$allVersions = $false       # Rebuild all image versions
)

#----------------------------------------------------------
# Global includes
$image_root = [System.IO.Path]::Combine($env:NK_ROOT, "Images")
. $image_root/includes.ps1
#----------------------------------------------------------

# Take care to ensure that you order the image builds such that
# dependant images are built before any dependancies.

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
                ./publish.ps1 -all -nopush
            }
            else
            {
                ./publish.ps1 -all
            }
        }
        else
        {
            if ($nopush)
            {
                ./publish.ps1 -nopush
            }
            else
            {
                ./publish.ps1
            }
        }
    }
    finally
    {
        Pop-Cwd | Out-Null
    }
}

try
{
    # Handle the command line arguments.

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

    # Purge any local Docker images as well as the image build cache.
    # This also purges all other Docker assets as a side effect.  We
    # need to do this to ensure to ensure a clean build.

    if (!$noprune)
    {
        $result = Invoke-CaptureStreams "docker system prune -af" -interleave
    }

    # NOTE: 
    #
    # The build order below is important since later images
    # may depend on earlier ones.

    if ($base)
    {
        # Base images: it's lonely here!
    }

    if ($other)
    {
        Publish "$image_root\couchbase-dev"
        Publish "$image_root\nats"
        Publish "$image_root\nats-streaming"
        Publish "$image_root\playground"
        Publish "$image_root\yugabyte"
    }

    if ($test)
    {
        Publish "$image_root\test"
        Publish "$image_root\test-cadence"
        Publish "$image_root\test-temporal"
    }

    if ($services)
    {
        Publish "$image_root\neon-acme"
        Publish "$image_root\neon-blazor-proxy"
        Publish "$image_root\neon-cluster-operator"
        Publish "$image_root\neon-dashboard"
        Publish "$image_root\neon-node-agent"
        Publish "$image_root\neon-sso-session-proxy"
        Publish "$image_root\test-api"
    }

    # Purge any local Docker images as well as the image build cache.
    # This also purges all other Docker assets as a side effect.
    #
    # We're doing this to ensure that Docker is reset to its default
    # state after building images.  This is especially important for
    # GitHub runners.

    if (!$noprune)
    {
        $result = Invoke-CaptureStreams "docker system prune -af" -interleave
    }
}
catch
{
    Write-Exception $_
    throw
}
