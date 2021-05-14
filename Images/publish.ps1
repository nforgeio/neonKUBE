#Requires -Version 7.0 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         publish.ps1
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

# Builds and publishes all of the Neon Docker images.
#
# NOTE: You must be logged into Docker Hub.
#
# USAGE: pwsh -file ./publish-all.ps1 [-all]

param 
(
    [switch]$all         = $false,      # Rebuild all images
    [switch]$base        = $false,      # Rebuild base images
    [switch]$test        = $false,      # Rebuild test related images
    [switch]$other       = $false,      # Rebuild all other images (usually script based)
    [switch]$services    = $false,      # Rebuild all cluster service images
    [switch]$nopush      = $false,      # Don't push to the registry
    [switch]$noprune     = $false,      # Don't prune the local Docker cache
    [switch]$allVersions = $false,      # Rebuild all image versions
    [switch]$rel         = $false,      # Override current branch and publish to: ghcr.io/neonrelease
    [switch]$dev         = $false       # Override current branch and publish to: ghcr.io/neonrelease-dev
)

#----------------------------------------------------------
# Global includes
$image_root = [System.IO.Path]::Combine($env:NF_ROOT, "Images")
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

    Set-Location "$Path"

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
    # This also purges everything else Docker as a side effect.  We
    # need to do this to ensure that we get a clean build.

    if (-not $noprune)
    {
        docker system prune -af
    }

    # NOTE: 
    #
    # The build order below is important since later images
    # may depend on earlier ones.

    if ($base)
    {
        # Base images: it's lonely here!
    }

Log-DebugLine "*** ROOT-PUBLISH: 0"
    if ($other)
    {
Log-DebugLine "*** ROOT-PUBLISH: 1"
        Publish "$image_root\\nats"
Log-DebugLine "*** ROOT-PUBLISH: 2"
        Publish "$image_root\\nats-streaming"
Log-DebugLine "*** ROOT-PUBLISH: 3"
        Publish "$image_root\\couchbase-dev"
Log-DebugLine "*** ROOT-PUBLISH: 4"
        Publish "$image_root\\yugabyte"
Log-DebugLine "*** ROOT-PUBLISH: 5"
    }

    if ($test)
    {
Log-DebugLine "*** ROOT-PUBLISH: 6"
        Publish "$image_root\\test"
Log-DebugLine "*** ROOT-PUBLISH: 7"
        Publish "$image_root\\test-cadence"
Log-DebugLine "*** ROOT-PUBLISH: 8"
        Publish "$image_root\\test-temporal"
Log-DebugLine "*** ROOT-PUBLISH: 9"
    }

    if ($services)
    {
Log-DebugLine "*** ROOT-PUBLISH: 10"
        Publish "$image_root\\neon-allow-testing"
Log-DebugLine "*** ROOT-PUBLISH: 11"
        Publish "$image_root\\neon-cluster-operator"
Log-DebugLine "*** ROOT-PUBLISH: 12"
        Publish "$image_root\\neon-setup-grafana"
Log-DebugLine "*** ROOT-PUBLISH: 13"
        Publish "$image_root\\neon-setup-harbor"
Log-DebugLine "*** ROOT-PUBLISH: 14"
    }
}
catch
{
    Write-Exception $_
    throw
}
