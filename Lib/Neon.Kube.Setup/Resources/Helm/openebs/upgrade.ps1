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

$version    = (Get-KubeVersion OpenEbs)
$tempFolder = [System.IO.Path]::Combine($env:TEMP, "openebs")
$helmFolder = [System.IO.Path]::Combine($env:NK_ROOT, "Lib", "Neon.Kube.Setup", "Resources", "Helm", "openebs")

# Remove the temporary folder if it still exists from a previous run..

if ([System.IO.Directory]::Exists($tempFolder))
{
    [System.IO.Directory]::Delete($tempFolder, $true)
}

# Pull and unpack the specified version of the Helm chart to the temporary
# folder.  NOTE: the subfolder created will be [openebs].

helm repo remove openebs
helm repo add openebs https://openebs.github.io/charts
helm repo update
helm pull openebs/openebs --version $version --destination $env:TEMP --untar

# Copy the upgrade instructions and a copy of this script to the temporary folder.

cp $helmFolder\NEONKUBE-README.md $tempFolder
cp $helmFolder\upgrade.ps1 $tempFolder

# Remove all of the [dependency.repository] properties recusively from all
# of the [v2] [chart.yaml] files within the temporary chart folder.  We need
# to do this to prevent Helm from downloading Helm charts from the Internet.

$helmMungerPath = Get-HelmMungerPath
Remove-HelmRepositories $tempFolder

# $hack(jefflill):
#
# We're having trouble with the [mayastor] dependency: Helm complains about
# its [loki-stack] subchart which doesn't make a lot of sense (something
# about a missing [Makefile] or something).
#
# It looks like [loki-stack] might have been deprecated so we're going to remove
# this subchart from the [mayastor] chart since we don't want to use it anyway
# since we have our own Loki deployment.
#
#       https://github.com/alexellis/arkade/issues/620

Remove-HelmDependency $tempFolder\charts\mayastor loki-stack

# Clear the OpenEBS source Helm folder and then copy in the unpacked
# Helm chart files, plus the upgrade instructions and script and then
# remove the temporary folder.

rm -r $helmFolder\*
cp -r $tempFolder\* $helmFolder
rm -r $tempFolder
