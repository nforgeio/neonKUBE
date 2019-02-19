# Publishes RELEASE builds of the NeonForge Nuget packages to the
# local file system and public Nuget.org repositories.

function SetVersion
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	text pack-version "$env:NF_ROOT\nuget-version.txt" "$env:NF_ROOT\Lib\$project\$project.csproj"
}

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Release -o "$env:NF_build\nuget"

	# Load the package semantic version number and strip off any build
    # or prerelease labels because [dotnet pack] strips these off the
    # nuget package files it builds.

	$version = Get-Content "$env:NF_ROOT\nuget-version.txt" -First 1
    $version = $version.Substring(0, $version.IndexOf('-')) # Removes any preview label
    $version = $version.Substring(0, $version.IndexOf('+')) # Removes any build metadata label

	# $todo(jeff.lill):
    #
    # We need to use [nshell run ...]  to retrieve the API key from an encrypted
    # secrets file rather than depending on NUGET_API_KEY environment variable
    # always being set.
    #
    #   https://github.com/nforgeio/neonKUBE/issues/448

	nuget push -Source nuget.org "$env:NF_BUILD\nuget\$project.$version.nupkg" %NUGET_API_KEY%
}

# Update the project versions first.

SetVersion Neon.Common
SetVersion Neon.Couchbase
SetVersion Neon.Cryptography
SetVersion Neon.Docker
SetVersion Neon.HyperV
SetVersion Neon.Kube
SetVersion Neon.Kube.Aws
SetVersion Neon.Kube.Azure
SetVersion Neon.Kube.Google
SetVersion Neon.Kube.Hosting
SetVersion Neon.Kube.HyperV
SetVersion Neon.Kube.HyperVLocal
SetVersion Neon.Kube.Machine
SetVersion Neon.Kube.XenServer
SetVersion Neon.Web
SetVersion Neon.XenServer
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Kube

# Build and publish the projects.

Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Cryptography
Publish Neon.Docker
Publish Neon.HyperV
Publish Neon.Kube
Publish Neon.Kube.Aws
Publish Neon.Kube.Azure
Publish Neon.Kube.Google
Publish Neon.Kube.Hosting
Publish Neon.Kube.HyperV
Publish Neon.Kube.HyperVLocal
Publish Neon.Kube.Machine
Publish Neon.Kube.XenServer
Publish Neon.HiveMQ
Publish Neon.Web
Publish Neon.XenServer
Publish Neon.Xunit
Publish Neon.Xunit.Kube
pause
