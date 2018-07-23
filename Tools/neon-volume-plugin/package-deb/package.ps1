#------------------------------------------------------------------------------
# FILE:         package.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# Creates a Debian package from the [neon-volume-plugin] build output.  This
# reads the package version number from [version.txt] and writes the package
# file to [${ProjectDir}bin/neon-volume-plugin_VERSION.deb].
#
# Usage: powershell -file package.ps1 PROJECT-PATH
#
# ARGUMENTS:
#
#	PROJECT-PATH	- Path to the [neon-volume-plugin] Visual Studio
#					  project folder).

param 
(
	[parameter(Mandatory=$True,Position=1)][string] $projectPath
)

# Remove any trailing slashes from project path.

$projectPath = $projectPath.TrimEnd("\\")
$projectPath = $projectPath.TrimEnd("/")

# Also remove a trailing double quote that seems to be added by 
# the project's post build event for some crazy reason.

$projectPath = $projectPath.TrimEnd('"')

# We'll be working in the project folder.

cd "$projectPath"

# Read the package version number from [version.go].  We're expecting
# this file to specify the version as a GO constant.

$versionPath    = Join-Path -Path $projectPath -ChildPath "version.go"
$versionPattern = '^\s*const\s*version\s*=\s*"(?<version>[\d\.]+)"'
$versionRegex   = [regex] $versionPattern
$versionLine    = Select-String -Path $versionPath -Pattern $versionRegex | foreach { $_.Line }

if ($versionLine -ne "")
{
    $matches = $versionRegex.Match($versionLine)
    $version = $matches.Groups["version"].Value
}
else
{
    Write-Error "*** ERROR: Cannot parse the plugin version number from [version.go]."
    exit 1
}

"   "
"============================================="
"* Packaging: neon-volume-plugin v" + $version
"============================================="

$version += "-1"    # Append the Debian package revision.

# The package is super simple right now.  We're not going to bother with
# pre/post install steps that manage enabling/starting the plugin service.
# This will initialize the systemd service file though.
#
# The link below describes what we're doing here:
#
#		https://ubuntuforums.org/showthread.php?t=910717
#
# The basic approach is to:
#
#	* Create a folder named [bin\neon-volume-plugin_VERSION].
#
#	* Populate this with the files/folders to install.
#
#	* ...and with the special [DEBIAN/control] file.
#
#   * Copy the [package.sh] script which will be
#     run within an Ubuntu container to actually build
#     the package.
#
#	* Launch Ubuntu mapping [PROJECT-PATH/bin] --> [/src] and
#     [package.sh] there.  This will generate the package
#	  [PROJECT-PATH/bin/neon-volume-plugin_VERSION.deb].
#
#	* The package will be copied to the solution build folder. 

#------------------------------------------------------------------------------
# Executes a command, throwing an exception for non-zero error codes.

function Exec
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [scriptblock]$Command,
        [Parameter(Position=1, Mandatory=0)]
        [string]$ErrorMessage = "*** FAILED: $Command"
    )
    & $Command
    if ($LastExitCode -ne 0) {
        throw "Exec: $ErrorMessage"
    }
}

#------------------------------------------------------------------------------
# Deletes a folder if it exists.

function DeleteFolder
{
    [CmdletBinding()]
    param (
        [Parameter(Position=0, Mandatory=1)]
        [string]$Path
    )

	if (Test-Path $Path) 
	{ 
		Remove-Item -Recurse $Path 
	} 
}

#------------------------------------------------------------------------------
# Steps to generate the package.

$binPath        = [io.path]::combine($projectPath, 'bin')
$packageName    = "neon-volume-plugin_$version"
$packageSrcPath = [io.path]::combine($binPath, $packageName)

# Initialize and populate the packager input folders and files.  Note that
# text files need to be converted to Linux line endings.

DeleteFolder "$packageSrcPath"
mkdir "$packageSrcPath"
mkdir "$packageSrcPath\\DEBIAN"
mkdir "$packageSrcPath\\lib\\neon\\bin"
mkdir "$packageSrcPath\\lib\\systemd\\system"

# Copy the Debian package control file, replacing PACKAGE_VERSION with the
# actual version number.

$controlText = Get-Content -Path "package-deb\\control.txt"
$controlText = $controlText -replace "PACKAGE_VERSION", $version
$controlText | Out-File "$packageSrcPath\\DEBIAN\\control"
exec { unix-text "$packageSrcPath\\DEBIAN\\control" }

# Copy the plugin binary.

copy "bin\neon-volume-plugin" "$packageSrcPath\\lib\\neon\\bin"

# Copy the systemd service file.

copy "package-deb\\neon-volume-plugin.service" "$packageSrcPath\\lib\\systemd\\system"
exec { unix-text "$packageSrcPath\\lib\\systemd\\system\\neon-volume-plugin.service" }

# We need to copy the [package.sh] script that will be launched 
# within the container to actually build the package.

copy "$projectPath\\package-deb\\package.sh" "$binPath"
exec { unix-text "$binPath\\package.sh" }

# Build the Debian package using an Ubuntu container.

docker pull nhive/ubuntu-16.04
docker run --rm -v "${binPath}:/src" nhive/ubuntu-16.04 bash /src/package.sh $packageName

# Copy the package to the solution build folder.

$packagePath = [io.path]::combine($binPath, "$packageName.deb")
copy "$packagePath" "$env:NF_BUILD"

# Copy it again as the latest version.

$targetPath = [io.path]::combine($env:NF_BUILD, "neon-volume-plugin_latest.deb")
copy "$packagePath" "$targetPath"
