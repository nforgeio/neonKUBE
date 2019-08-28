#------------------------------------------------------------------------------
# Provisions the services and VMs required to host a Redis cluster on Azure.
# 
# Modify the variables below to vary the hosting DC (Location), service
# and VM names prefixes, as well as the number of nodes.  Also, modify the login 
# credentials, disk size and count.
#
# This script provisions a single service that hosts the Redis data nodes.
#
# Redis Service
# -------------
# This script can create one or more VMs to be used to host the Redis nodes.
# By convention, these VMs will be named with a "rds" prefix followed by the 
# zero-based serverID, like:
#
#     jeffli-wus-rds-0
#     jeffli-wus-rds-1
#     jeffli-wus-rds-2
#
# These VMs will be provisioned behind a load balancer exposing external SSH
# ports will be mapped to internal SSH port 22 on each VM.  Redis endpoints
# are not exposed externally for production clusters.  This means that any other
# services consuming Redis will need to be deployed on the same VNET.
#
#     jeffli-wus-rds-0     SSH    external=22000, internal=22
#     jeffli-wus-rds-1     SSH    external=22001, internal=22
#     jeffli-wus-rds-2     SSH    external=22002, internal=22
#
# For development purposes, it is possible to expose the endpoint externally
# by setting [$externalEP = true].  This will map external port 60000 + serverID
# to the internal standard Redis port 6379.
#
#     jeffli-wus-rds-0     REDIS  external=60000, internal=6379
#     jeffli-wus-rds-1     REDIS  external=60001, internal=6379
#     jeffli-wus-rds-2     REDIS  external=60002, internal=6379
#
# Set [$replicate=$true] to deploy a replicated cluster where each master node
# will have a backup slave.  This script will assign each node to one of two
# Azure availability groups with nodes with even IDs going into one group and
# those with odd IDs into the other.  The deployer will configure the Redis
# cluster such that the master and slave for each hash slot will be hosted
# in different availability groups.

# Common parameters

$subscription          = "AMP Common Infrastructure - Dev"
$adminUser             = "iceman"
$adminPassword         = "Thin.ICE"
$serviceNamePrefix     = "jeffli-wus"
$location              = "West US"
$defaultVmImage        = "0b11de9248dd4d87b18621318e037d37__RightImage-Ubuntu-14.04-x64-v14.2.1"
$endpointIdleTimeout   = 20                  # Azure endpoint timeout in min (30 max)
$vnetName              = "jeffli-wus-rds"
$subnetName            = "WUS-RDS"
$externalEP            = $false              # Set [$true] for to expose endpoint for non-production

# DATA VM parameters

$dataStorageAccount    = "jeffliwus"		 # Must be premium storage for DS VMs
$dataImageName         = $defaultVmImage
$dataVmSize            = "Standard_D13"      # Must be DS series for premium storage
$replicate             = $false              # Deploy slave nodes for each master
$nodeCount             = 30                  # Must be even if [$replicate=true]
$diskCount             = 0                   # Options: 1..4
$diskSizeGB            = 1023                # Options: 128, 512, 1023
$diskHostCaching       = "None"              # Options: None, ReadOnly, ReadWrite

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

$externalEP            = $false

$vnetName              = "jeffli-wus-cdb"
$subnetName            = "WUS-CDB"

# $location              = "West Europe"
# $serviceNamePrefix     = "csidev-weu"
# $opsStorageAccount     = "csidevweu"
# $dataStorageAccount    = "csidevweumdb"
# $vnetName              = "jeffli-weu-cdb"
# $subnetName            = "WEU-CDB"
# $dcVnetId              = 4

# $dataStorageAccount    = "jeffliwus"
# $location              = "West US"
# $serviceNamePrefix     = "jeffli-g"
# $dataVmSize            = "Standard_G1"
# $diskCount             = 0

$dataServiceName       = $serviceNamePrefix + "-rds"
$dataVmNamePrefix      = $dataServiceName + "-"

function Provision-DataVM(
    [string] $vmInstanceName,
    [int]    $nodeId) {

    $sshPort       = 22000 + $nodeId
    $redisPort     = 60000 + $nodeId
    $bootDiskPath  = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"
    $vnetIP        = "10.0.0." + ($nodeId + 4);

    # If replicas are enabled, even node IDs will be created in availablity set 
    # "Rack-0" and those with odd IDs in "Rack-1".

    if ($replicate) {
        
        if ($nodeId % 2 -eq 0) {
        
            $rackId = 0
        }
        else {

            $rackId = 1
        } 
    }
    else {

        $rackId = 0
    }
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $dataImageName -InstanceSize $dataVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "rack-$rackId" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Set-AzureSubNet -SubnetNames $subnetName |
          Set-AzureStaticVNETIP -IPAddress $vnetIP

    if ($externalEP) {

        $vm = $vm | Add-AzureEndpoint -Name "REDIS" -Protocol tcp -LocalPort 6379 -PublicPort $redisPort -IdleTimeoutInMinutes $endpointIdleTimeout
    }

    for ($i=0; $i -lt $diskCount; $i++){

        $lunNo        = $i + 1
        $dataDiskPath = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/DATA-" + $vm.RoleName + "-" + $lunNo + ".vhd"
        $label        = "Disk " + $lunNo

        Add-AzureDataDisk -CreateNew -MediaLocation $dataDiskPath -DiskSizeInGB $diskSizeGB -DiskLabel $label -LUN $lunNo -HostCaching $diskHostCaching -VM $vm
    }

    New-AzureVM -ServiceName $dataServiceName -location $location -VMs $vm -VNETName $vnetName
}

#------------------------------------------------------------------------------
# Main code

# Add-AzureAccount
Set-AzureSubscription -SubscriptionName $subscription -CurrentStorageAccount $dataStorageAccount
Select-AzureSubscription -SubscriptionName $subscription

# Verify the parmaeters.

if ($replicate) {

    if ($nodeCount % 2 -ne 0) {

        Write-Error "[node-count] must be even if [replicas=true]."
        return
    }
}

# Remove the data service if it already exists.

$serviceRemoved = $false

Write-Output ""

foreach ($service in Get-AzureService) {

    if ($service.ServiceName -eq $dataServiceName) {

        $serviceRemoved = $true
        Write-Output "Removing DATA service: $dataServiceName"
        Remove-AzureService -ServiceName $service.ServiceName -DeleteAll -Force
    }
}

if ($serviceRemoved) {
    
    # Azure holds a lease on VM disk images for a period of
    # time after the the service and its VMs have been deleted,
    # so we need to wait 5 minutes to allow the leases to 
    # expire before attempting to reprovision the service.

    Write-Output "Waiting 5 minutes to allow VM disk leases to expire..."
    Start-Sleep -s 300
}

Write-Output ""
Write-Output "Provisioning REDIS VMs for: $dataServiceName"
Write-Output "-------------------------------------------------"

for($nodeId=0; $nodeId -lt $nodeCount; $nodeId++){
 
    $vmInstanceName = $dataVmNamePrefix + $nodeId
 
    Write-Output "REDIS VM: $vmInstanceName"
    Provision-DataVM $vmInstanceName $nodeId
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
