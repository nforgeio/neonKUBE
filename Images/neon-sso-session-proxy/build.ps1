#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         build.ps1
# CONTRIBUTOR:  Marcus Bowyer
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
#
# Builds the Neon [neon-sso-session-proxy] image.
#
# USAGE: pwsh -f build.ps1 REGISTRY VERSION TAG

param 
(
	[parameter(Mandatory=$true, Position=1)][string] $registry,
	[parameter(Mandatory=$true, Position=2)][string] $tag,
	[parameter(Mandatory=$true, Position=3)][string] $config
)

$appname      = "neon-sso-session-proxy"
$organization = SdkRegistryOrg

try
{
    # Build and publish the app to a local [bin] folder.

    DeleteFolder bin

    mkdir bin | Out-Null
    ThrowOnExitCode

    dotnet publish "$nkServices\$appname\$appname.csproj" -c $config -o "$pwd\bin" -p:SolutionName=$env:SolutionName
    ThrowOnExitCode

    # Split the build binaries into [__app] (application) and [__dep] dependency subfolders
    # so we can tune the image layers.

    core-layers $appname "$pwd\bin"
    ThrowOnExitCode

    # Build the image.

    $baseImage = Get-DotnetBaseImage "$nkRoot\global.json"

    Invoke-CaptureStreams "docker build -t ${registry}:${tag} --build-arg `"APPNAME=$appname`" --build-arg `"ORGANIZATION=$organization`" --build-arg `"BASE_IMAGE=$baseImage`" ." -interleave | Out-Null
}
finally
{
    # Clean up

    DeleteFolder bin
}
