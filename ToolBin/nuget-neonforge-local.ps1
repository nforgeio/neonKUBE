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
SetVersion Neon.Kube
SetVersion Neon.Kube.Aws
SetVersion Neon.Kube.Azure
SetVersion Neon.Kube.Google
SetVersion Neon.Kube.Hosting
SetVersion Neon.Kube.HyperV
SetVersion Neon.Kube.HyperVDev
SetVersion Neon.Kube.Machine
SetVersion Neon.Kube.Xen
SetVersion Neon.HiveMQ
SetVersion Neon.HyperV
SetVersion Neon.Web
SetVersion Neon.Xen
SetVersion Neon.Xunit
SetVersion Neon.Xunit.Couchbase
SetVersion Neon.Xunit.RabbitMQ
SetVersion Neon.Xunit.Kube

# Build and publish the projects.

Publish Neon.Common
Publish Neon.Kube
Publish Neon.Kube.Aws
Publish Neon.Kube.Azure
Publish Neon.Kube.Google
Publish Neon.Kube.Hosting
Publish Neon.Kube.HyperV
Publish Neon.Kube.HyperVDev
Publish Neon.Kube.Machine
Publish Neon.Kube.Xen
Publish Neon.HiveMQ
Publish Neon.HyperV
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.Web
Publish Neon.Xen
Publish Neon.Xunit
Publish Neon.Xunit.Couchbase
Publish Neon.Xunit.RabbitMQ
Publish Neon.Xunit.Kube
pause
