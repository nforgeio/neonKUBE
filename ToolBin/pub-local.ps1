# Publishes RELEASE builds of the library NuGet packages to the local
# file system at: %NF_BUILD%\nuget.

# Take care to ensure that you order the image builds such that
# dependant images are built before any dependancies.

function Publish
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$project
    )

	text pack-version "$env:NF_ROOT\\nuget-version.txt" "$env:NF_ROOT\\Lib\\$project\\$project.csproj"
	dotnet pack "$env:NF_ROOT\\Lib\\$project\\$project.csproj" -o "$env:NF_build\\nuget"
}

Publish Neon.Cluster
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.RabbitMQ
Publish Neon.Xunit
