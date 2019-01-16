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

	# Load the package version number.

	$version = Get-Content "$env:NF_ROOT\nuget-version.txt" -First 1

	# We need to run [nuget push] in the context of [neon run] so we can
	# reference the NuGet API key from the encrypted [secrets.yaml] file.

	neon run --vault-password-file=neon-git "$env:NF_ROOT\Devops\test\secrets.yaml" -- nuget push -Source nuget.org "$env:NF_BUILD\nuget\$project.$version.nupkg" %NUGET_API_KEY%
}

# Update the project version numbers first.

SetVersion Neon.Common
SetVersion Neon.Couchbase
SetVersion Neon.Docker
SetVersion Neon.Kube
SetVersion Neon.Kube.Aws
SetVersion Neon.Kube.Azure
SetVersion Neon.Kube.Google
SetVersion Neon.Kube.Hosting
SetVersion Neon.Kube.HyperV
SetVersion Neon.Kube.HyperVDev
SetVersion Neon.Kube.Machine
SetVersion Neon.Kube.XenServer
SetVersion Neon.HiveMQ
SetVersion Neon.Web
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.RabbitMQ
SetVersion Neon.Xunit.HKubeive

# Build and publish the projects.

Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.Kube
Publish Neon.Kube.Aws
Publish Neon.Kube.Azure
Publish Neon.Kube.Google
Publish Neon.Kube.Hosting
Publish Neon.Kube.HyperV
Publish Neon.Kube.HyperVDev
Publish Neon.Kube.Machine
Publish Neon.Kube.XenServer
Publish Neon.HiveMQ
Publish Neon.Web
Publish Neon.Xunit
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.RabbitMQ
Publish Neon.Xunit.Kube
pause
