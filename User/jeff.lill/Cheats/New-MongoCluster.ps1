#------------------------------------------------------------------------------
# Provisions the services and VMs required to host a MongoDB cluster on Azure.
# 
# Modify the variables below to vary the hosting DC (Location), service
# and VM names prefixes, as well as the number of shards and replicas
# per shard set.  Also, modify the login credentials, disk size and
# count.
#
# This script provisions two services: a ROUTER service that hosts the
# Mongos query routers and a MDB service to host the MongoDB data servers.
# Two services are required because a single service cannot run Azure DS
# series VMs along with standard VM types.
#
# MongoDB ROUTER Service
# -----------------------
# This script can create zero more VMs to be used to host the Mongos
# routing service.  By convention, these VMs will be named with a prefix
# ending with "mqr" (for Mongo Query Router) followed by the zero-based 
# serverID, like:
#
#     jeffli-wus-mqr-0
#     jeffli-wus-mqr-1
#     jeffli-wus-mqr-2
#
# These VMs will be provisioned behind a service load-balanced TCP endpoint
# on port 27017 mapping to local port 27017 for database requests.  HTTP
# health probes will be configured to hit local port 80.  SSH will be configured
# with external port 22000 + serverID mapping to local port 22.
#
#     jeffli-wus-mqr-0     SSH   external=22000, internal=22
#                          MONGO external=27017, internal=27017 (load balanced)
#
#     jeffli-wus-mqr-1     SSH   external=22001, internal=22
#                          MONGO external=27017, internal=27017 (load balanced)
#
#     jeffli-wus-mqr-2     SSH   external=22002, internal=22
#                          MONGO external=27017, internal=27017 (load balanced)
#
#
# MongoDB MDB Service
# -------------------
# The MDB service name will include the "-mdb" suffix (for MongoDB).
#
# This script will create data VMs within the service that will be used 
# to host the shards and replica sets as well as the replica set configuration
# databases.  As a convention, these VMs will have name prefixes that end
# with "d" for data followed by the shard name, (a letter like "a", "b", "c",...
# end then the zero-based replica number, like:
#
#     SHARD: a
#     --------
#     jeffli-wus-mdb-a0
#     jeffli-wus-mdb-a1
#     jeffli-wus-mdb-a2
#
#     SHARD: b
#     --------
#     jeffli-wus-mdb-b0
#     jeffli-wus-mdb-b1
#     jeffli-wus-mdb-b2
#
# TCP endpoint mappings are generated SSH, MongoDB (data), and MongoDB (shard config)
# server instances.  SSH mappings begin at 22100, MongoDB (data) mappings at 27000,
# and MongoDB (shard config) mappings at 28000.  50 ports in each range are allocated
# to a specific shard.  For example, for shard "a" SSH ports will be allocated from
# 27000-27049 and for shard "b", ports will be allocated from 27050-27099,... with 
# ports being assigned to servers by shard server number.
#
# Here's how external SSH ports will be assigned to the shard servers above:
#
#     SHARD: a
#     --------
#     jeffli-wus-mdb-a0    SSH external=22000, internal=22
#     jeffli-wus-mdb-a1    SSH external=22001, internal=22
#     jeffli-wus-mdb-a2    SSH external=22002, internal=22
#
#     SHARD: b
#     --------
#     jeffli-wus-mdb-b0    SSH external=22051, internal=22
#     jeffli-wus-mdb-b1    SSH external=22051, internal=22
#     jeffli-wus-mdb-b2    SSH external=22051, internal=22
#
# Port mappings for MongoDB data and config servers will be allocated the same way,
# with all MongoDB data endpoints mapping internally to the same port and all MongoDB 
# config endpoints mapping internally to 27019 (default config server port).  Here's 
# what this will look like:
#
#     SHARD: a
#     --------
#     jeffli-wus-mdb-a0    Mongo DATA external=27000, internal=27000
#                          Mongo CONF external=28000, internal=27019
#     jeffli-wus-mdb-a1    Mongo DATA external=27001, internal=27001
#                          Mongo CONF external=28001, internal=27019
#     jeffli-wus-mdb-a2    Mongo DATA external=27002, internal=27002
#                          Mongo CONF external=28002, internal=27019
#
#     SHARD: b
#     --------
#     jeffli-wus-mdb-b0    Mongo DATA external=27050, internal=27000
#                          Mongo CONF external=28050, internal=27019
#     jeffli-wus-mdb-b1    Mongo DATA external=27051, internal=27001
#                          Mongo CONF external=28051, internal=27019
#     jeffli-wus-mdb-b2    Mongo DATA external=27052, internal=27001
#                          Mongo CONF external=28052, internal=27019
#
# These port gymnastics are required because Windows Azure limits availability
# set functionality to Azure Cloud Services where the service VMs are all
# behind a common VIP.  The script below creates each shard set in their
# own availability set/cloud service to ensure that shard servers are
# provisioned properly across Azure fault and upgrade domains.
#
# Note also that I'd prefer to map the internal DATA ports to the 27017
# default but TokuMK 2.0 can't find itself when configuring a replica
# set when the internal and external ports don't match exactly. 
#
# -----------------------------------------------------------------------------
# MAXIMUM CAPACITY NOTE
#
# Windows Azure currently supports a maximum of 150 endpoint definitions per
# cloud service VIP.  Since the MDB service currently configures three endpoints
# per VM, services are currently limited to 50 VMs.  This could be increased 
# somewhat by allocating Mongo CONF only for VMs that are actually hosting 
# production configuration servers (MongoDB requires three shard configuration
# databases per cluster for clusters that implement sharding).

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
$routerCount           = 4
$routerPublicPort      = 27017

# DATA VM parameters

$dataStorageAccount    = "jeffliwusmdb"      # Must be premium storage for DS VMs
$dataImageName         = $defaultVmImage
$dataVmSize            = "Standard_DS4"      # Must be DS series for premium storage
$shardCount            = 3
$replicaCount          = 3
$diskCount             = 1                   # Options: 1..4
$diskSizeGB            = 1023                # Options: 128, 512, 1023
$diskHostCaching       = "None"              # Options: None, ReadOnly, ReadWrite

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

# $location              = "West Europe"
# $serviceNamePrefix     = "csidev-weu"manage
# $routerStorageAccount  = "csidevweu"
# $dataStorageAccount    = "csidevweumdb"

$dataStorageAccount    = "jeffliwus"
$location              = "West US"
$serviceNamePrefix     = "jeffli-g"
$dataVmSize            = "Standard_G1"
$diskCount             = 0

$routerServiceName     = $serviceNamePrefix + "-mqr"
$routerVmNamePrefix    = $routerServiceName + "-"

$dataServiceName       = $serviceNamePrefix + "-mdb"
$dataVmNamePrefix      = $dataServiceName + "-"

function Provision-RouterVM(
    [string] $vmInstanceName,
    [int]    $serverId) {

    $sshPort      = 22000 + $serverId
    $bootDiskPath = "https://" + $routerStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"

	# TODO: Provision SSL
    # TODO: Modify the ROUTER endpoint to enable VIP health probes.
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $routerImageName -InstanceSize $routerVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "routers" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "ROUTER" -LBSetName "ROUTER" -NoProbe -Protocol tcp -LocalPort 27017 -PublicPort $routerPublicPort -IdleTimeoutInMinutes $endpointIdleTimeout 

    New-AzureVM -ServiceName $routerServiceName -location $location -VMs $vm
}

function Provision-DataVM(
    [string] $vmInstanceName,
    [string] $shardName,
    [int]    $shardId,
    [int]    $replicaId) {

	# TODO: Provision SSL

    $sshPort      = 22000 + ($shardId * 50) + $replicaId
    $mongodPort   = 27000 + ($shardId * 50) + $replicaId
    $mongocPort   = 28000 + ($shardId * 50) + $replicaId
    $bootDiskPath = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $dataImageName -InstanceSize $dataVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "shard-$shardName" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "MongoDB-DATA" -Protocol tcp -LocalPort $mongodPort -PublicPort $mongodPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "MongoDB-CONF" -Protocol tcp -LocalPort 27019 -PublicPort $mongocPort -IdleTimeoutInMinutes $endpointIdleTimeout 

    for ($i=0; $i -lt $diskCount; $i++){

        $lunNo        = $i + 1
        $dataDiskPath = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/DATA-" + $vm.RoleName + "-" + $lunNo + ".vhd"
        $label        = "Disk " + $lunNo

        Add-AzureDataDisk -CreateNew -MediaLocation $dataDiskPath -DiskSizeInGB $diskSizeGB -DiskLabel $label -LUN $lunNo -HostCaching $diskHostCaching -VM $vm
    }

    New-AzureVM -ServiceName $dataServiceName -location $location -VMs $vm
}

#------------------------------------------------------------------------------
# Main code

# Add-AzureAccount
Set-AzureSubscription -SubscriptionName $subscription -CurrentStorageAccount $dataStorageAccount
Select-AzureSubscription -SubscriptionName $subscription

# Remove the router and/or data services if they already exist.

$serviceRemoved = $false

Write-Output ""

foreach ($service in Get-AzureService) {

    if ($service.ServiceName -eq $routerServiceName) {

        $serviceRemoved = $true
        Write-Output "Removing ROUTER service: $routerServiceName"
        Remove-AzureService -ServiceName $service.ServiceName -DeleteAll -Force
    }
    elseif ($service.ServiceName -eq $dataServiceName) {

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
Write-Output "Provisioning ROUTER VMs for: $routerServiceName"
Write-Output "-------------------------------------------------"

for($serverId=0; $serverId -lt $routerCount; $serverId++){

    $vmInstanceName = $routerVmNamePrefix + $serverId;

    Write-Output "ROUTER VM: $vmInstanceName"
    Provision-RouterVM $vmInstanceName $serverId
}

Write-Output ""
Write-Output "Provisioning DATA VMs for: $dataServiceName"
Write-Output "-------------------------------------------------"

for($shardId=0; $shardId -lt $shardCount; $shardId++){

    for($replicaId=0; $replicaId -lt $replicaCount; $replicaId++){
 
        $shardName      = [string][char]([int][char]'a' + $shardId)
        $vmInstanceName = $dataVmNamePrefix + $shardName + $replicaId
 
        Write-Output "DATA VM: $vmInstanceName"
        Provision-DataVM $vmInstanceName $shardName $shardId $replicaId
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
