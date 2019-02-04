#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and

# Builds the cluster [neon-cli] image.
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
