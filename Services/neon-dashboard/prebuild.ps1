 #Requires -Version 7
 
 <#
    FILE: prebuild.ps1
    CONTRIBUTOR: Simon Zhang
    COPYRIGHT: Copyright (c) 2005-2022 by neonFORGE LLC. All rights reserved.

    this file checks for installed npm package, and installs if not. 
    *notably faster then running "npm install" 
    BUT this script doesnt check for proper version numbers, only if it exists
 #>

#comment out this line to silence output
$InformationPreference = 'Continue'

$devdep = (Get-Content package.json) -join "`n" | ConvertFrom-Json -AsHashtable | Select -ExpandProperty "devDependencies"
$_, $installedList = npm list --depth=0 

$shouldRunInstall = $FALSE
Write-Information -MessageData ('[NPM Package Check]-----------') 
$count = 0
foreach ($key in $devdep.keys)
{
  $testKey = "*"+$key+"*"
  if($installedList -like $testKey ) {
    #Write-Information -MessageData ('   - '+$Key+' is installed')

    $count++
  } else {
    shouldRunInstall = $TRUE
    Write-Information -MessageData ($key+' is not installed on this machine >_>') 
    break;
  }
}

if ($shouldRunInstall) {
  Write-Information -MessageData "Installing node packages ^_^" 
  npm i
} else {
  Write-Information -MessageData ('['+$count+'/'+($installedList.Length -1)+'] npm packages found ^_^ continuing build') 
}
Write-Information -MessageData ('------------------------------') 