# Publishes DEBUG builds of the NeonForge Nuget packages to the local
# file system at: %NF_BUILD%\nuget.

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

	dotnet pack "$env:NF_ROOT\Lib\$project\$project.csproj" -c Debug --include-symbols --include-source -o "$env:NF_build\nuget"
}

# Update the project versions first.

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
SetVersion Neon.HyperV
SetVersion Neon.Web
SetVersion Neon.Xen
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.RabbitMQ
SetVersion Neon.Xunit.Hive

# Build and publish the projects.

Publish Neon.Common
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
Publish Neon.HyperV
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.Web
Publish Neon.Xen
Publish Neon.Xunit
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.RabbitMQ
Publish Neon.Xunit.Hive
pause
