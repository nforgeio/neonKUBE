#Requires -Version 7.1.3 -RunAsAdministrator
#------------------------------------------------------------------------------
# FILE:         upgrade.ps1
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
#
# The contents of this repository are for private use by NEONFORGE, LLC. and may not be
# divulged or used for any purpose by other organizations or individuals without a
# formal written and signed agreement with NEONFORGE, LLC.

#----------------------------------------------------------
# Global Includes
$image_root = "$env:NK_ROOT\Images"
. $image_root/includes.ps1
#----------------------------------------------------------

$version        = (Get-KubeVersion Cilium)
$tempFolder     = [System.IO.Path]::Combine($env:TEMP, "cilium")
$helmFolder     = [System.IO.Path]::Combine($env:NK_ROOT, "Lib", "Neon.Kube.Setup", "Resources", "Helm", "cilium")
$helmMungerPath = Get-HelmMungerPath

# Remove any leading "v" from the version.

if ($version.StartsWith("v"))
{
    $version = $version.SubString(1)
}

# Remove the temporary folder if it still exists from a previous run.

if ([System.IO.Directory]::Exists($tempFolder))
{
    [System.IO.Directory]::Delete($tempFolder, $true)
}

# Create the temporary folder.

[System.IO.Directory]::CreateDirectory($tempFolder)
Push-Location $tempFolder

# Handle the chart download and munging.

try
{
    # Fetch the Helm [.tgz] file from GitHub and extract it to the
    # temporary folder.

    curl -4fsSL --retry 10 --retry-delay 30 --max-redirs 10 https://raw.githubusercontent.com/cilium/charts/master/cilium-$version.tgz > $tempFolder\chart.tgz
    tar -xvzf $tempFolder\chart.tgz
    rm $tempFolder\chart.tgz
    mv $tempFolder\cilium\* $tempFolder
    rm cilium

    # Copy the upgrade instructions and a copy of this script to the temporary folder.

    cp $helmFolder\NEONKUBE-README.md $tempFolder
    cp $helmFolder\upgrade.ps1 $tempFolder

    # Remove all of the [dependency.repository] properties recusively from all
    # of the [v2] [chart.yaml] files within the temporary chart folder.  We need
    # to do this to prevent Helm from downloading Helm charts from the Internet.

    Remove-HelmRepositories $tempFolder
}
finally
{
    Pop-Location
}

# Clear the Cilium source Helm folder and then copy in the unpacked
# Helm chart files, plus the upgrade instructions and script and then
# remove the temporary folder.

rm -r $helmFolder\*
cp -r $tempFolder\* $helmFolder
rm -r $tempFolder
