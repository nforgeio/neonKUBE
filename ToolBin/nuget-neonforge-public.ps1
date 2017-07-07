# Publishes DEBUG builds of the NeonForge Nuget packages to the
# local file system and public Nuget.org repositories.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	text pack-version "$env:NF_ROOT\nuget-version.txt" "$env:NF_ROOT\Lib\$project\$project.csproj"
	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Release -o "$env:NF_build\nuget"

	$version = Get-Content "$env:NF_ROOT\nuget-version.txt" -First 1
	nuget push "$env:NF_BUILD\nuget\$project.$version.nupkg" $env:NF_NUGET_API_KEY
}

Publish Neon.Cluster
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.RabbitMQ
Publish Neon.Xunit
