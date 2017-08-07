# Publishes DEBUG builds of the NeonForge Nuget packages to the local
# file system at: %NF_BUILD%\nuget.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	text pack-version "$env:NF_ROOT\nuget-version.txt" "$env:NF_ROOT\Lib\$project\$project.csproj"
	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NF_build\nuget"
}

Publish Neon.Cluster
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.RabbitMQ
Publish Neon.Xunit
pause
