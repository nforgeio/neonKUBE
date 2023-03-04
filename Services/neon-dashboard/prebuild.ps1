 #Requires -Version 7
 
 <#
    FILE: prebuild.ps1
    CONTRIBUTOR: Simon Zhang
    COPYRIGHT: Copyright © 2005-2022 by NEONFORGE LLC. All rights reserved.

    this file checks for installed npm package, and installs if not. 
    *notably faster then running "npm install" 
    BUT this script doesnt check for proper version numbers, only if it exists
 #>

#comment out this line to silence output
$InformationPreference = 'Continue'

Write-Information -MessageData ('[NPM Package Check] Checking dependencies.') 

$devdep = (Get-Content package.json) -join "`n" | ConvertFrom-Json -AsHashtable | Select -ExpandProperty "devDependencies"
$_, $installedList = npm list --location=project --silent

$shouldRunInstall = $false
if($LastExitCode -eq 1)
{
  Write-Information -MessageData "[NPM Package Check] Installing node packages ^_^" 
  npm install --save --silent
}
else{
  Write-Information -MessageData ('[NPM Package Check] npm packages found ^_^ continuing build') 
}

npm run build