#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Builds the neonHIVE [neon-cli] image.
#
# Usage: powershell -file build.ps1 [-latest]
#
# Note that the build script retrieves the version number from the tool itself.

param 
(
	[switch]$latest = $False
)

"   "
"======================================="
"* NEON-CLI"
"======================================="

$appname      = "neon"
$registry     = GetRegistry "neon-cli"
$organization = DockerOrg
$branch       = GitBranch

# Build and publish the app to a local [bin] folder.

if (Test-Path bin)
{
	rm -r bin
}

Exec { mkdir bin }
Exec { dotnet publish "$src_tools_path\\neon-cli\\neon-cli.csproj" -c Release -o "$pwd\bin" }

# Split the build binaries into [__app] application and [__dep] dependency subfolders
# so we can tune the image layers.

Exec { core-layers $appname "$pwd\bin" }

# Invoke the tool to retrieve its version number.

$version=$(& dotnet "$pwd\bin\$appname.dll" version -n)

# Build the image.

if (IsProd)
{
	Exec { docker build -t "${registry}:$version" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" --build-arg "APPNAME=$appname" . }
	PushImage "${registry}:$version"

	Exec { docker tag "${registry}:$version" "${registry}:$branch-$version"}
	PushImage "${registry}:$branch-$version"

	if ($latest)
	{
		Exec { docker tag "${registry}:$version" "${registry}:latest"}
		PushImage "${registry}:latest"

		Exec { docker tag "${registry}:$version" "${registry}:${branch}-latest" }
		PushImage "${registry}:${branch}-latest"
	}
}
else
{
	Exec { docker build -t "${registry}:$branch-$version" --build-arg "ORGANIZATION=$organization" --build-arg "BRANCH=$branch" --build-arg "APPNAME=$appname" . }
	PushImage "${registry}:$branch-$version"

	if ($latest)
	{
		Exec { docker tag "${registry}:$branch-$version" "${registry}:$branch-latest"}
		PushImage "${registry}:$branch-latest"
	}
}
