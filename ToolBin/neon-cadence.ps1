#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         neonkube-cadence.ps1
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

# Maintains the http://github.com/nforgeio/cadence repository by adding
# new Cadence release branches from the source http://github.com/uber/cadence
# repository.
#
# USAGE: pwsh -file ./neon-cadence.ps1 PATH BRANCH
#
# COMMANDS:
#
#       sync master         - Synchronizes our cloned MASTER branch with
#                             the original Uber Cadence MASTER and pushes
#                             the changes to our forked branch on GitHub.
#
#       release BRANCH      - Creates a new local copy of the specified 
#                             branch from the original Uber Cadence repository
#                             and pushes the changes to our forked branch
#                             on GitHub.
#
# REQUIREMENTS:
#
# You must have http://github.com/nforgeio/cadence cloned to [%NK_REPOS/neon-cadence].
#
# REMARKS:
#
# The [release] command expects BRANCH to be the name of the source branch from
# the original Uber Cadence repository.  Note that Cadence branches named like
# [0.5.8_release] are still in progress and have not yet been released.

param 
(
	[Parameter(Position=0, mandatory=$true)]
    [string] $command,
	[Parameter(Position=1, mandatory=$true)]
    [string] $branch
)

# Import the global solution include file.

. $env:NK_ROOT/Powershell/includes.ps1

# Initialize

$nfRepos = "$env:NK_REPOS"

# Ensure that there's a local [neon-cadence] repository in NK_REPOS.

if (!(Test-Path "$nfRepos/neon-cadence/.git/index"))
{
    Write-Error -Message "[neon-cadence] repository does not exist at: $nfRepos/neon-cadence"
    exit 1
}

Push-Cwd "$nfRepos\neon-cadence" | Out-Null

switch ($command)
{
    "sync" 
    {
        git checkout master
        git pull https://github.com/uber/cadence.git master
        git push origin master
        break
    }

    "release"
    {
        git fetch https://github.com/uber/cadence.git ${branch}:${branch}
        git checkout $branch
        git push -u origin $branch
        git push -u origin
        break
    }

    default
    {
        Write-Error -Message "Unknown command: ${command}"
        Pop-Cwd | Out-Null
        exit 1
    }
}

Pop-Cwd | Out-Null

