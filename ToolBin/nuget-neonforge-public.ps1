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

	# It looks like [dotnet pack] doesn't include a zero revision number when
	# nameing the output file.  So {Neon.Common] built as version [0.0.5.0] will
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

	nuget push -Source nuget.org "$env:NF_BUILD\nuget\$project.$version.nupkg" $env:NF_NUGET_API_KEY
}

Publish Neon.Cluster
Publish Neon.Common
Publish Neon.Couchbase
Publish Neon.Docker
Publish Neon.RabbitMQ
Publish Neon.Web
Publish Neon.Xunit
pause
