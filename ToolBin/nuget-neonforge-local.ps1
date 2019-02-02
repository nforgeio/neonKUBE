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
Publush Neon.Couchbase
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
