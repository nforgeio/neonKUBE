# Publishes DEBUG builds of the NeonForge Nuget packages to the
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

	# It looks like [dotnet pack] doesn't include a zero revision number when
	# naming the output file.  So {Neon.Common] built as version [0.0.5.0] will
	# generate [Neon.Common.0.0.5.nupkg] not [Neon.Common.0.0.5.0.nupkg].
	#
	# We need to strip the last ".0" off the version string in this case,
	# so [nuget push] will be able to find the file.

	$version = Get-Content "$env:NF_ROOT\nuget-version.txt" -First 1
	$fields  = [array]$version.Split('.')

	if ($fields.Length -eq 4 -and $version.EndsWith(".0"))
	{
		$version = $version.Substring(0, $version.Length - 2)
	}

	# We need to run [nuget push] in the context of [neon run] so we can
	# reference the NuGet API key from the encrypted [secrets.yaml] file.

	neon run --vault-password-file=neon-git "$env:NF_ROOT\Devops\test\secrets.yaml" -- nuget push -Source nuget.org "$env:NF_BUILD\nuget\$project.$version.nupkg" %NUGET_API_KEY%
}

# Update the project version numbers first.

SetVersion Neon.Cluster
SetVersion Neon.Common
SetVersion Neon.Couchbase
SetVersion Neon.Docker
SetVersion Neon.RabbitMQ
SetVersion Neon.Web
SetVersion Neon.Xunit

# Then build and publish the projects.

Publish Neon.Cluster
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.RabbitMQ
Publish Neon.Web
Publish Neon.Xunit
pause
