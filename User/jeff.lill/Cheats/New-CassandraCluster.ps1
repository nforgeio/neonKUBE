#------------------------------------------------------------------------------
# Provisions the services and VMs required to host a Cassandra cluster on Azure.
# 
# Modify the variables below to vary the hosting DC (Location), service
# and VM names prefixes, as well as the number of nodes.  Also, modify the login 
# credentials, disk size and count.
#
# This script provisions a single service that hosts the Cassandra data nodes
# along with one Cassandra OpsCenter instance.
#
# Cassandra Service
# -----------------
# This script can create one or more VMs to be used to host the Cassandra nodes.
# By convention, these VMs will be named with a "cdb" prefix followed by the 
# zero-based serverID, like:
#
#     jeffli-wus-cdb-0
#     jeffli-wus-cdb-1
#     jeffli-wus-cdb-2
#
# The OpsCenter instance will be named with the "-cops" suffix, like:
#
#     jeffli-wus-cops
#
# These VMs will be provisioned behind a load balancer with no external access to
# the Cassandra node endpoints allowed.  External port 80 of the service will be 
# mapped to local port 80 where the OpCenter UX will be listening.  External SSH
# ports will be mapped to internal SSH port 22 on each VM.
#
# NOTE: To access OpsCenter use a URL like:
#
#     http://jeffli-wus-cops.cloudapp.net/
#
#     *** Default credentials:   UID: "admin", PWD: "admin"
#     *** IE is kind of broken:  Use Firefox or Chrome instead.
#
# SSH will be configured with external port 22253 mapping to the OpCenter instance
# local port 22 and 22000 + serverID mapping to local port 22 of the data nodes.
#
#     jeffli-wus-cops      SSH    external=22253, internal=22
#                          HTTP   external=80,    internal=80
#
#     jeffli-wus-cdb-0     SSH    external=22000, internal=22
#     jeffli-wus-cdb-1     SSH    external=22001, internal=22
#     jeffli-wus-cdb-2     SSH    external=22002, internal=22
#
# VNET Subnet Layout
# ------------------
# Cassandra requires that all node VMs be individually addressable by IP
# address using the same API ports on all nodes: 7000 for internode
# communication and 9042 for native protocol clients.  This means that it
# is not possible to deploy Cassandra on Azure without assigning public
# static VNET IP addresses to each node and then ensuring that these IPs 
# don't change to avoid manually patching the node configuration.
#
# Note also the Cassandra client drivers (such as the C# driver) also need
# to access these same node IP addresses and ports.  The drivers are generally
# initialized with a set of seed IP addresses and from this, discover the
# deployment topology.  The driver is then able to direct traffic at specific
# nodes, hopefully avoiding extra network hops and reducing internal network
# traffic.
#
# The consequence of this is that services calling Cassandra will need to be
# provisioned within the same VNET or be bridged in via a VNET-to-VNET connection.
# A Cassandra cluster deployed to multiple datacenters will also need to have
# VNET bridging configured so the nodes will be able to reach each other across
# DC boundaries.
#
# To accomplish this, we'll need to standardize on how we layout VNET subnets
# to avoid addressing conflicts.  IP ranges will be assigned to as:
#
#     10.<datacenter>.<service>.<machine>
#
# where:    <datacenter>     - identifies the datacenter
#           <service>        - identifies the service
#           <machine>        - identifies a specific machine
#
# Cassandra clusters will standardize on  class C addressing within the 10.0.0.0/8 
# address space.  The actual Cassandra nodes will be organized into 10.#.#.0/24 
# subnets by datacenter as shown below :
#
#     Cassandra Nodes (service #0)
#     ----------------------------
#     10.0.0.0/24    - WUS-CDB  (CDB = Cassandra DB)
#     10.1.0.0/24    - EUS-CDB
#     10.2.0.0/24    - NEU-CDB
#     10.3.0.0/24    - EAS-CDB
#     10.4.0.0/24    - WEU-CDB
#     10.5.0.0/24    - SUS-CDB
#     10.6.0.0/24    - CUS-CDB
#
# This script will assign static VNET IP subnet addresses to both the OpsCenter instance
# as well as each of the data nodes,  The OpsCenter node will always be assigned the
# 10.#.0.253 address on the subnet and the data nodes will be assigned addresses by node 
# ID as: 10.#.0.<node ID + 4>
#
#     10.0.0.253:    jeffli-wus-cops  ** Note: Azure reserves the *.254 and *.255 addresses
#     
#     10.0.0.4:      jeffli-wus-cdb-0
#     10.0.0.5:      jeffli-wus-cdb-1
#     10.0.0.6:      jeffli-wus-cdb-2
#       ...                ...
#
# Other services that need access to the data nodes (such as a public REST endpoint
# or a MONITOR service) will be assigned to subnets like:
#
#     REST (service #1)
#     ----------------------
#     10.0.1.0/24    - WUS-REST
#     10.1.1.0/24    - EUS-REST
#     10.2.1.0/24    - NEU-REST
#     10.3.1.0/24    - EAS-REST
#     10.4.1.0/24    - WEU-REST
#     10.5.1.0/24    - SUS-REST
#     10.6.1.0/24    - CUS-REST
#
#     MONITOR (service #2)
#     ----------------------
#     10.0.2.0/24    - WUS-MON
#     10.1.2.0/24    - EUS-MON
#     10.2.2.0/24    - NEU-MON
#     10.3.2.0/24    - EAS-MON
#     10.4.2.0/24    - WEU-MON
#     10.5.2.0/24    - SUS-MON
#     10.6.2.0/24    - CUS-MON
#
# Note the subnet naming convention and also that the second number in each
# address specifies the same datacenter for all of these subnets.  Additional
# datacenters and services can be supported using the same pattern.
#
# Subnets for ad-hoc test related services (like load testers) will be configured
# from the top end of the address range (to avoid conflicting with production
# services).
#
#     TEST (service #255)
#     ----------------------
#     10.0.255.0/24  - WUS-TEST
#     10.1.255.0/24  - EUS-TEST
#     10.2.255.0/24  - NEU-TEST
#     10.3.255.0/24  - EAS-TEST
#     10.4.255.0/24  - WEU-TEST
#     10.5.255.0/24  - SUS-TEST
#     10.6.255.0/24  - CUS-TEST
#
# The next test service would be assigned the 10.#.254.# subnet, etc.

# Common parameters

$subscription          = "AMP Common Infrastructure - Dev"
$adminUser             = "iceman"
$adminPassword         = "Thin.ICE"
$serviceNamePrefix     = "jeffli-wus"
$location              = "West US"
$defaultVmImage        = "0b11de9248dd4d87b18621318e037d37__RightImage-Ubuntu-14.04-x64-v14.2.1"
$endpointIdleTimeout   = 30                  # Azure endpoint timeout in min (30 max)
$vnetName              = "jeffli-wus-cdb"
$subnetName            = "WUS-CDB"
$dcVnetId              = 0

# OPS VM Parameters

$opsStorageAccount     = "jeffliwus"         # Must be premium storage for DS VMs
$opsImageName          = $defaultVmImage
$opsVmSize             = "Standard_D1"       # Must be DS series for premium storage

# DATA VM parameters

$dataStorageAccount    = "jeffliwusmdb"      # Must be premium storage for DS VMs
$dataImageName         = $defaultVmImage
$dataVmSize            = "Standard_D3"       # Must be DS series for premium storage
$rackCount             = 3                   # Should be an integer divisor of [$nodeCount]
$nodeCount             = 1
$diskCount             = 0                   # Options: 1..4
$diskSizeGB            = 1023                # Options: 128, 512, 1023
$diskHostCaching       = "None"              # Options: None, ReadOnly, ReadWrite

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

# $location              = "West Europe"
# $serviceNamePrefix     = "csidev-weu"
# $opsStorageAccount     = "csidevweu"
# $dataStorageAccount    = "csidevweumdb"
# $vnetName              = "jeffli-weu-cdb"
# $subnetName            = "WEU-CDB"
# $dcVnetId              = 4

$dataStorageAccount    = "jeffliwus"
# $location              = "West US"
# $serviceNamePrefix     = "jeffli-g"
# $dataVmSize            = "Standard_G1"
# $diskCount             = 0

$opsServiceName        = $serviceNamePrefix + "-cops"

$dataServiceName       = $serviceNamePrefix + "-cdb"
$dataVmNamePrefix      = $dataServiceName + "-"

function Provision-OpsCenterVM() {

	# TODO: Provision SSL

    $vmInstanceName = $opsServiceName
    $sshPort        = 22253
    $bootDiskPath   = "https://" + $opsStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"
    $vnetIP         = "10.$dcVnetId.0.253"
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $opsImageName -InstanceSize $opsVmSize -MediaLocation $bootDiskPath |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "OPSCENTER" -Protocol tcp -LocalPort 80 -PublicPort 80 -IdleTimeoutInMinutes $endpointIdleTimeout |
          Set-AzureSubNet -SubnetNames $subnetName |
          Set-AzureStaticVNETIP -IPAddress $vnetIP

    New-AzureVM -ServiceName $dataServiceName -location $location -VMs $vm -VNETName $vnetName
}

function Provision-DataVM(
    [string] $vmInstanceName,
    [int]    $nodeId) {

    $sshPort       = 22000 + $nodeId
    $interNodePort = 7000 + $nodeId
    $bootDiskPath  = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"
    $rackId        = $nodeId % $rackCount
    $vnetIP        = "10.$dcVnetId.0." + ($nodeId + 4);
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $dataImageName -InstanceSize $dataVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "rack-$rackId" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Set-AzureSubNet -SubnetNames $subnetName |
          Set-AzureStaticVNETIP -IPAddress $vnetIP

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

# Remove the service if it already exists.

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
Write-Output "Provisioning OPSCENTER VM for: $dataServiceName"
Write-Output "-------------------------------------------------"

Write-Output "DATA VM: $opsServiceName"
Provision-OpsCenterVM

Write-Output ""
Write-Output "Provisioning DATA VMs for: $dataServiceName"
Write-Output "-------------------------------------------------"

for($nodeId=0; $nodeId -lt $nodeCount; $nodeId++){
 
    $vmInstanceName = $dataVmNamePrefix + $nodeId
 
    Write-Output "DATA VM: $vmInstanceName"
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
