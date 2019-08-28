#------------------------------------------------------------------------------
# Starts the virtual machines in a Mongo cluster.

# Common parameters

$subscription          = "AMP Common Infrastructure - Dev"
$adminUser             = "iceman"
$adminPassword         = "Thin.ICE"
$serviceNamePrefix     = "jeffli-wus"
$location              = "West US"
$defaultVmImage        = "0b11de9248dd4d87b18621318e037d37__RightImage-Ubuntu-14.04-x64-v14.2"
$endpointIdleTimeout   = 30                  # Azure endpoint timeout in min (30 max)

# ROUTER VM parameters

$routerStorageAccount  = "jeffliwus"
$routerImageName       = $defaultVmImage
$routerVmSize          = "Large" 
$routerCount           = 1
$routerPublicPort      = 27017

# DATA VM parameters

$dataStorageAccount    = "jeffliwusmdb"      # Must be premium storage for DS VMs
$dataImageName         = $defaultVmImage
$dataVmSize            = "Standard_DS4"      # Must be DS series for premium storage
$shardCount            = 1
$replicaCount          = 3
$diskCount             = 1                   # Options: 1..4
$diskSizeGB            = 1023                # Options: 128, 512, 1023
$diskHostCaching       = "None"              # Options: None, ReadOnly, ReadWrite

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

# $location              = "West Europe"
# $serviceNamePrefix     = "csidev-weu"
# $routerStorageAccount  = "csidevweu"
# $dataStorageAccount    = "csidevweumdb"

$routerServiceName     = $serviceNamePrefix + "-mqr"
$routerVmNamePrefix    = $routerServiceName + "-"

$dataServiceName       = $serviceNamePrefix + "-mdb"
$dataVmNamePrefix      = $dataServiceName + "-"

#------------------------------------------------------------------------------
# Main code

# Add-AzureAccount
Set-AzureSubscription -SubscriptionName $subscription -CurrentStorageAccount $dataStorageAccount
Select-AzureSubscription -SubscriptionName $subscription

Write-Output ""
Write-Output "Starting ROUTER VMs for: $routerServiceName"
Write-Output "-------------------------------------------------"

for($serverId=0; $serverId -lt $routerCount; $serverId++){

    $vmInstanceName = $routerVmNamePrefix + $serverId;

    Write-Output "Starting: $vmInstanceName"
	Start-AzureVM -Name $vmInstanceName -ServiceName $routerServiceName
}

Write-Output ""
Write-Output "Starting DATA VMs for: $dataServiceName"
Write-Output "-------------------------------------------------"

for($shardId=0; $shardId -lt $shardCount; $shardId++){

    for($replicaId=0; $replicaId -lt $replicaCount; $replicaId++){
 
        $shardName      = [string][char]([int][char]'a' + $shardId)
        $vmInstanceName = $dataVmNamePrefix + $shardName + $replicaId
 
		Write-Output "Starting: $vmInstanceName"
		Start-AzureVM -Name $vmInstanceName -ServiceName $dataServiceName
    }
}

# Wait for any jobs to complete and cleanup

While (Get-Job -State "Running") { 

    Get-Job -State "Completed" | Receive-Job
    Start-Sleep 1
}

Get-Job | Receive-Job
Remove-Job *

Write-Output ""
Write-Output "*** DONE ***"
Write-Output ""
