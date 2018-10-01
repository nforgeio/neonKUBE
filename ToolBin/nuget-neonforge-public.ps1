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
SetVersion Neon.Hive
SetVersion Neon.Hive.Aws
SetVersion Neon.Hive.Azure
SetVersion Neon.Hive.Google
SetVersion Neon.Hive.Hosting
SetVersion Neon.Hive.HyperV
SetVersion Neon.Hive.HyperVDev
SetVersion Neon.Hive.Machine
SetVersion Neon.Hive.Xen
SetVersion Neon.HiveMQ
SetVersion Neon.Web
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.RabbitMQ
SetVersion Neon.Xunit.Hive

# Build and publish the projects.

Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.Hive
Publish Neon.Hive.Aws
Publish Neon.Hive.Azure
Publish Neon.Hive.Google
Publish Neon.Hive.Hosting
Publish Neon.Hive.HyperV
Publish Neon.Hive.HyperVDev
Publish Neon.Hive.Machine
Publish Neon.Hive.Xen
Publish Neon.HiveMQ
Publish Neon.Web
Publish Neon.Xunit
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.RabbitMQ
Publish Neon.Xunit.Hive
pause
