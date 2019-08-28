#------------------------------------------------------------------------------
# Provisions the services and VMs required to host an ElasticSearch cluster on Azure.
#
# The script creates two services, one holding the routers and the other holding
# the Elasticsearch nodes.  The routers VMs host Linux Nginx, an HTTP reverse proxy.
# These are used to provide a level of security for the Elasticsearch nodes which
# don't provide for authentication or authorization out of the box.
#
# The router service name will include the [-esr] suffix (for Elasticsearch Router)
# and the node service name will include the [-esd] suffix (for Elasticsearch Data).
# VMs are named with the service name plus a zero based [-#] suffix specifiying the 
# VM instance number.  For example:
#
#     Router Service (jeffli-wus-esr):
#     --------------------------------
#     jeffli-wus-esr-0     -- VM0
#     jeffli-wus-esr-1     -- VM1
#
#     Data Service (jeffli-wus-esd):
#     ------------------------------
#     jeffli-wus-esd-0     -- VM0
#     jeffli-wus-esd-1     -- VM1
#     jeffli-wus-esd-2     -- VM2
#     jeffli-wus-esd-3     -- VM3
#
# The router instances host:
#
#     Elasticsearch - Non-data, non-master nodes that act as gateways to the
#                     to the data cluster.
#
#     Kibana        - Node based analytics web UI.
#
#     Nginx         - Reverse proxy configured to layer security on top of
#                     Kibana and Elasticsearch.
#
# The router instances expose HTTP Kibana ports on 80/443 and the Elasticsearch API
# on port 9200 through an Azure load balancer. The Elasticsearch data nodes expose no
# public Elasticsearch endpoints.  The data node API is reachable only via the routers
# who are on the same VNET as the data nodes and will use their local VNET IP addresses
# for communication.
#
#     Router VM Port Mappings:
#
#     External       Local      Service
#     -------------------------------------------------------------
#         80           80       Nginx: Proxy to Kitana:5601
#        443          443       Nginx: Proxy to Kitana:5601
#        666         9200       Nginx: Proxy to Elasticsearch:9200 (HTTP API)
#
#     Data VM Port Mappings:
#
#     External       Local      Service
#     -------------------------------------------------------------
#       -na-         9200       Elasticsearch (HTTP API)
#       -na-         9300       Elasticsearch (TCP API)
#
# This script exposes SSH ports for both router and Elasticsearch node VMs.  SSH
# ports will be mapped to specific VMs for each service.  Port 22000 will be mapped 
# to the first VM in each service, 22001 to the second, and so on.
#
# The script also creates both the router and node VMs within an Azure VNET.
# The VNET will be named with the [-es] suffix, like [jeffli-wus-es] and it
# will be configured with two */24 subnets:
#
#     VNET Subnets
#    ------------
#     Router: 10.0.0.0/24
#     Data:   10.0.1.0/24
#
# Individual VMs will be assigned local IP addresses within their respective 
# subnets by instance number, starting with address 10.0.x.4 within the subnet
# (addresses 10.0.x.0 .. 3 are reserved by Azure).  This scheme allows up to 
# 252 router and 252 Elasticsearch VMs to be deployed.  The tables below depict
# example local IP address and SSH port assignments.
#
#     Router Service (jeffli-wus-esr):
#     --------------------------------
#     jeffli-wus-esr-0     -- IP=10.0.0.4 SSH=22000
#     jeffli-wus-esr-1     -- IP=10.0.0.5 SSH=22001
#
#     Data Service (jeffli-wus-esd):
#     ------------------------------
#     jeffli-wus-esd-0     -- IP=10.0.1.4 SSH=22000
#     jeffli-wus-esd-1     -- IP=10.0.1.5 SSH=22001
#     jeffli-wus-esd-2     -- IP=10.0.1.6 SSH=22002
#     jeffli-wus-esd-3     -- IP=10.0.1.7 SSH=22003
#

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
$routerVmSize          = "Medium" 
$routerCount           = 1

# DATA VM parameters

$dataStorageAccount    = "jeffliwusmdb"      # Must be premium storage for DS VMs
$dataImageName         = $defaultVmImage
$dataVmSize            = "Standard_DS3"      # Must be DS series for premium storage
$nodeCount             = 1
$diskCount             = 1                   # Options: 1..4
$diskSizeGB            = 128                 # Options: 128, 512, 1023
$diskHostCaching       = "None"              # Options: None, ReadOnly, ReadWrite

# -----------------------------------------------
# @DEPLOYER-OVERRIDES@
# -----------------------------------------------

$vnetName              = $serviceNamePrefix + "-es"

$routerServiceName     = $serviceNamePrefix + "-esr"
$routerVmNamePrefix    = $routerServiceName + "-"

$dataServiceName       = $serviceNamePrefix + "-esd"
$dataVmNamePrefix      = $dataServiceName + "-"

function Provision-RouterVM(
    [string] $vmInstanceName,
    [int]    $routerId) {

    $sshPort      = 22000 + $routerId
    $bootDiskPath = "https://" + $routerStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"

	# TODO: Provision SSL
    # TODO: Modify the ROUTER endpoint to enable VIP health probes.

    $ipSeg = 4 + $routerId
 
    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $routerImageName -InstanceSize $routerVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "routers" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Set-AzureSubnet -SubnetNames "Router" | 
          Set-AzureStaticVNetIP -IPAddress "10.0.0.$ipSeg" |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "KIBANA-80" -LBSetName "KIBANA-80" -NoProbe -Protocol tcp -LocalPort 80 -PublicPort 80 -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "KIBANA-443" -LBSetName "KIBANA-443" -NoProbe -Protocol tcp -LocalPort 443 -PublicPort 443 -IdleTimeoutInMinutes $endpointIdleTimeout |
          Add-AzureEndpoint -Name "ELASTIC-API" -LBSetName "ELASTIC-API" -NoProbe -Protocol tcp -LocalPort 666 -PublicPort 666 -IdleTimeoutInMinutes $endpointIdleTimeout

    New-AzureVM -ServiceName $routerServiceName -location $location -VMs $vm -VNetName $vnetName
}

function Provision-DataVM(
    [string] $vmInstanceName,
    [int]    $nodeId) {

    $sshPort      = 22000 + $nodeId
    $bootDiskPath = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/BOOT-" + $vmInstanceName + ".vhd"
 
	# TODO: Provision SSL
    # TODO: Remove the KABANA port once routers are working

    $ipSeg = 4 + $nodeId

    $vm = New-AzureVMConfig -Name $vmInstanceName -ImageName $dataImageName -InstanceSize $dataVmSize -MediaLocation $bootDiskPath -AvailabilitySetName "data-nodes" |
          Add-AzureProvisioningConfig -Linux -LinuxUser $adminUser -Password $adminPassword |
          Set-AzureSubnet -SubnetNames "Data" | 
          Set-AzureStaticVNetIP -IPAddress "10.0.1.$ipSeg" |
          Remove-AzureEndpoint -Name "SSH" |
          Add-AzureEndpoint -Name "SSH" -Protocol tcp -LocalPort 22 -PublicPort $sshPort -IdleTimeoutInMinutes $endpointIdleTimeout

    for ($i=0; $i -lt $diskCount; $i++){

        $lunNo        = $i + 1
        $dataDiskPath = "https://" + $dataStorageAccount + ".blob.core.windows.net/vhds/DATA-" + $vm.RoleName + "-" + $lunNo + ".vhd"
        $label        = "Disk " + $lunNo

        Add-AzureDataDisk -CreateNew -MediaLocation $dataDiskPath -DiskSizeInGB $diskSizeGB -DiskLabel $label -LUN $lunNo -HostCaching $diskHostCaching -VM $vm
    }

    New-AzureVM -ServiceName $dataServiceName -location $location -VMs $vm -VNetName $vnetName
}

#------------------------------------------------------------------------------
# Main code

# Add-AzureAccount
Set-AzureSubscription -SubscriptionName $subscription -CurrentStorageAccount $dataStorageAccount
Select-AzureSubscription -SubscriptionName $subscription
# Set-AzureVNetConfig -ConfigurationPath c:\temp\vnet.xml

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

# TODO: Need to write a PowerShell cmdlet that will download and
#       modify the subscription's VNET definition and then apply
#       it back to Azure.  The standard Azure PowerShell commands
#       manage all of the VNETs with a single config.
#
#       For now, we'll simply manage the VNET manually.

# -------------------------------------
# IMPORTANT!!!!! 
#
# Be sure to download and edit the current config to ensure that
# you're haven't blown away somebody else's VNET configuration
# changes for the subscription.
#
# Or manage the VNET in the portal.
# -------------------------------------

Write-Output ""
Write-Output "Provisioning ROUTER VMs for: $serviceNamePrefix"
Write-Output "-------------------------------------------------"

for($routerId=0; $routerId -lt $routerCount; $routerId++){

    $vmInstanceName = $routerVmNamePrefix + $routerId;

    Write-Output "ROUTER VM: $vmInstanceName"
    Provision-RouterVM $vmInstanceName $routerId
}

Write-Output ""
Write-Output "Provisioning DATA VMs for: $serviceNamePrefix"
Write-Output "-------------------------------------------------"

Set-AzureSubscription -SubscriptionName $subscription -CurrentStorageAccount $dataStorageAccount
Select-AzureSubscription -SubscriptionName $subscription

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
