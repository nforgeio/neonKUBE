//-----------------------------------------------------------------------------
// FILE:	    AzureHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.Network.Fluent.LoadBalancer.Definition;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Neon.Net;

using AzureEnvironment = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

// $todo(jeff.lill):
//
// I stubbed out Azure calls that weren't compiling due to changes
// in the Azure class library (I've been using a very old version).

#pragma warning disable 169 
#pragma warning disable 649 

#if AZURE_STUBBED
#endif

namespace Neon.Cluster
{
    /// <summary>
    /// Manages cluster provisioning on Microsoft Azure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The current implementation is focused on creating a cluster reachable from the
    /// external Internet via a load balancer or application gateway.  All Azure assets
    /// will be created within a specified resource group by appending a suffix to the
    /// neonCLUSTER's name.
    /// </para>
    /// </remarks>
    public partial class AzureHostingManager : HostingManager
    {
        // IMPLEMENTATION NOTE:
        // --------------------
        // The cluster is deployed behind an Azure load balancer which is configured to forward
        // TCP traffic on specified frontend ports to an internal port on any cluster node.
        // neonCLUSTERs will encounter three types of network traffic:
        //
        //      1. We provision the cluster in two available sets: one for the manager nodes
        //         and the other for the workers.  This is required because we need to tightly
        //         control the Azure update and fault domains for the managers so we'll always
        //         have a quorum.  Putting managers together with a lot of workers will make
        //         it pretty likely that a quorum of managers might fall into the same update
        //         domain and be taken out at the same time.
        //
        //      2. We provision the cluster with two load balancers, one for the managers and
        //         the other for the workers.  This is required because a single Azure load
        //         balancer is unable to server traffic for VMs in different availability sets.
        //         Each load balancer is assigned a public IP address.  Once the cluster is 
        //         configured, the manager load balancer only handles VPN traffic and the worker 
        //         load balancer handles application traffic.
        //
        //      3. We provision two network security groups: one for the NICs attached to the
        //         the manager load balancer and the other for NICs attached to the worker 
        //         load balancer.
        //
        //      4. Provisioning SSH traffic: Early on during the Azure provisioning process, 
        //         the [neon] tool needs to SSH into the new nodes before OpenVPN is deployed.
        //         This is done by configuring the load balancers to map an unique frontend port
        //         to each node's SSH port 22.  This allows the [neon] tool to SSH into a specific 
        //         node by using the assigned frontend port.
        //
        //         The [neon] tool assigns these ports, starting by default at port [37105] (after 
        //         the VPN NAT ports) to each node sorted in ascending order by node type (managers
        //         first)  and then node name.   These load balancer mappings will be deleted once
        //         the cluster has been fully provisioned.
        //
        //      5. VPN traffic: neonCLUSTERs deploy an internal VPN, with the cluster manager
        //         nodes running OpenVPN running and listening on it's standard port (1194).
        //         The Azure load balancer will be configured to NAT TCP connections from 
        //         reserved frontend ports starting at [37100] by default to internal port 1194 
        //         for each manager.
        //
        //      6. Application service traffic.  This is the website, webapi, and TCP traffic
        //         to your application services.  This traffic will be load balanced to every
        //         worker node, mapping the frontend port to an internal one.  Typically,
        //         the PUBLIC neonCLUSTER proxy will be configured to receive the inbound
        //         traffic on the Docker ingress network and forward it on to your Docker services.
        //
        //      7. It is possible to assign a public IP address to each cluster node.  This can
        //         help eliminate NAT port exhaustion in the load balancer for large clusters making
        //         a lot of outbound requests.  Note though that inbound traffic will still 
        //         route through the load balancers.
        //
        // Azure limits its load balancers to a maximum of 150 port mapping rules.  To make
        // things easy, we're going to limit neonCLUSTERs deployed to Azure to 100 nodes and 150 
        // hosted frontend application endpoints.  This means that we can have one SSH NAT rule
        // for each node during setup and one OpenVPN NAT rule for each manager node.
        //
        // In the future, it could be possible to relax the limits on node and perhaps application
        // endpoints as well, by provisioning the nodes in batches and recycling the SSH rules as 
        // we finish one batch of nodes and then start working on the next.

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to indicate whether to update worker, manager, or all network
        /// load balancer and network security rules.
        /// </summary>
        [Flags]
        private enum NetworkUpdateSets
        {
            /// <summary>
            /// Update all network load balancer and security rules.
            /// </summary>
            All = Worker | Manager,

            /// <summary>
            /// Update the worker load balancer and security rules.
            /// </summary>
            Worker = 0x00000001,

            /// <summary>
            /// Update the manager load balancer and security rules.
            /// </summary>
            Manager = 0x0000010
        }

        /// <summary>
        /// Relates cluster node information with Azure VM information.
        /// </summary>
        private class AzureNode
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="node">The associated node proxy.</param>
            public AzureNode(SshProxy<NodeDefinition> node)
            {
                this.Node = node;
            }

            /// <summary>
            /// Returns the associated node proxy.
            /// </summary>
            public SshProxy<NodeDefinition> Node { get; private set;}

            /// <summary>
            /// Returns the node name.
            /// </summary>
            public string Name
            {
                get { return Node.Name; }
            }

            /// <summary>
            /// The associated Azure VM.
            /// </summary>
            public IVirtualMachine Vm { get; set; }

            /// <summary>
            /// The node's network interface within the <b>nodes</b> subnet.
            /// </summary>
            public INetworkInterface NodesNic { get; set; }

            /// <summary>
            /// The node's network interface within the <b>vpn</b> subnet (managers only).
            /// </summary>
            public INetworkInterface VpnNic { get; set; }

            /// <summary>
            /// The node's Azure public IP address or FQDN (or <c>null</c>).
            /// </summary>
            public IPublicIPAddress PublicAddress { get; set; }

            /// <summary>
            /// The public FQDN or IP address (as a string) to be used to connect to the
            /// node via SSH while provisioning the cluster.  This will be set to the
            /// FQDN of <see cref="PublicAddress"/> if the cluster nodes are being
            /// provisioned with addresses or else the FQDN address of the cluster manager
            /// or worker load balancer.
            /// </summary>
            public string PublicSshAddress { get; set; }

            /// <summary>
            /// The SSH port to to be used to connect to the node via SSH while provisioning
            /// the cluster.  This will be the standard <see cref="NetworkPorts.SSH"/> if the 
            /// cluster nodes are being provisioned with addresses or else a temporary NAT port
            /// configured on the appropriate load balancer.
            /// </summary>
            public int PublicSshPort { get; set; } = NetworkPorts.SSH;

            /// <summary>
            /// Returns the Azure name for the temporary NAT rule mapping a 
            /// frontend load balancer port to the SSH port for this node.
            /// </summary>
            public string SshNatRuleName
            {
                get { return $"neon-ssh-tcp-{Node.Name}"; }
            }

            /// <summary>
            /// Returns the Azure name for the NAT rule mapping a frontend load balancer port to 
            /// the OpenVPN port for this node.
            /// </summary>
            public string VpnNatRuleName
            {
                get { return $"neon-vpn-tcp-{Node.Name}"; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a manager.
            /// </summary>
            public bool IsManager
            {
                get { return Node.Metadata.IsManager; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a worker.
            /// </summary>
            public bool IsWorker
            {
                get { return Node.Metadata.IsWorker; }
            }

            /// <summary>
            /// Returns <c>true</c> if the node is a pet.
            /// </summary>
            public bool IsPet
            {
                get { return Node.Metadata.IsPet; }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string managerLeafDomainPrefix = "vpn-";

        private ClusterProxy                    cluster;
        private HostingOptions                  hostOptions;
        private NetworkOptions                  networkOptions;
        private AzureOptions                    azureOptions;
        private Dictionary<string, AzureNode>   nodeDictionary;
        private string                          clusterName;
        private string                          resourceGroup;
        private AzureCredentials                azureCredentials;
        private IAzure                          azure;
        private string                          pipLbManagerName;
        private string                          pipLbWorkerName;
        private IPublicIPAddress                pipLbManager;
        private IPublicIPAddress                pipLbWorker;
        private string                          subnetNodesName;
        private string                          subnetVpnName;
        private string                          vpnRouteName;
        private string                          vnetName;
        private INetwork                        vnet;
        private string                          lbManagerName;
        private string                          lbWorkerName;
        private ILoadBalancer                   lbManager;
        private ILoadBalancer                   lbWorker;
        private string                          asetManagerName;
        private string                          asetWorkerName;
        private IAvailabilitySet                asetManager;
        private IAvailabilitySet                asetWorker;
        private string                          nsgVpnName;
        private string                          nsgNodeName;
        private INetworkSecurityGroup           nsgVpn;
        private INetworkSecurityGroup           nsgNode;
        private string                          feConfigName;
        private string                          bePoolName;
        private string                          probeName;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AzureHostingManager(ClusterProxy cluster, string logFolder = null)
        {
#if !AZURE_STUBBED
            throw new NotImplementedException("AzureHostingManager is currently stubbed.");
#endif
#if AZURE_STUBBED
            this.cluster        = cluster;
            this.clusterName    = cluster.Definition.Name;
            this.hostOptions    = cluster.Definition.Hosting;
            this.networkOptions = cluster.Definition.Network;
            this.azureOptions   = hostOptions.Azure;
            this.resourceGroup  = azureOptions.ResourceGroup;

            // Generate the Azure asset names for the cluster.

            this.pipLbManagerName = $"{clusterName}-pip-lb-manager";
            this.pipLbWorkerName  = $"{clusterName}-pip-lb-worker";
            this.vpnRouteName     = $"{clusterName}-vnet-routes";
            this.vnetName         = $"{clusterName}-vnet";
            this.lbManagerName    = $"{clusterName}-lb-manager";
            this.lbWorkerName     = $"{clusterName}-lb-worker";
            this.asetManagerName  = $"{clusterName}-aset-manager";
            this.asetWorkerName   = $"{clusterName}-aset-worker";
            this.nsgVpnName       = $"{clusterName}-nsg-vpn";
            this.nsgNodeName      = $"{clusterName}-nsg-node";
            this.subnetNodesName  = "nodes";
            this.subnetVpnName    = "vpn";
            this.feConfigName     = "frontend";
            this.bePoolName       = "backend";
            this.probeName        = "probe";

            // Associate the hosting manager and cluster.

            cluster.HostingManager = this;

            // Initialize the node mapping dictionary and also ensure
            // that each node has Azure node options.

            this.nodeDictionary = new Dictionary<string, AzureNode>();

            foreach (var node in cluster.Nodes)
            {
                nodeDictionary.Add(node.Name, new AzureNode(node));

                if (node.Metadata.Azure == null)
                {
                    // Initialize reasonable defaults.

                    node.Metadata.Azure = new AzureNodeOptions();
                }
            }
#endif
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            // The Azure connection class doesn't implement IDispose 
            // so we don't have to do anything here.

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Connects to Azure if we're not already connected.
        /// </summary>
        private void AzureConnect()
        {
#if AZURE_STUBBED
            if (azure != null)
            {
                return; // Already connected.
            }

            var environment = AzureEnvironment.AzureGlobalCloud;

            if (azureOptions.Environment != null)
            {
                switch (azureOptions.Environment.Name)
                {
                    case AzureCloudEnvironments.GlobalCloud:

                        environment = AzureEnvironment.AzureGlobalCloud;
                        break;

                    case AzureCloudEnvironments.ChinaCloud:

                        environment = AzureEnvironment.AzureChinaCloud;
                        break;

                    case AzureCloudEnvironments.GermanCloud:

                        environment = AzureEnvironment.AzureGermanCloud;
                        break;

                    case AzureCloudEnvironments.USGovernment:

                        environment = AzureEnvironment.AzureUSGovernment;
                        break;

                    case AzureCloudEnvironments.Custom:

                        environment = new AzureEnvironment()
                        {
                            AuthenticationEndpoint  = azureOptions.Environment.AuthenticationEndpoint,
                            GraphEndpoint           = azureOptions.Environment.GraphEndpoint,
                            ManagementEnpoint       = azureOptions.Environment.ManagementEnpoint,
                            ResourceManagerEndpoint = azureOptions.Environment.ResourceManagerEndpoint
                        };
                        break;

                    default:

                        throw new NotImplementedException($"Azure environment [{azureOptions.Environment.Name}] is not currently supported.");
                }
            }

            azureCredentials =
                new AzureCredentials(
                    new ServicePrincipalLoginInformation()
                    {
                        ClientId     = azureOptions.ApplicationId,
                        ClientSecret = azureOptions.Password
                    },
                azureOptions.TenantId,
                environment);

            azure = Azure.Configure()
                .Authenticate(azureCredentials)
                .WithSubscription(azureOptions.SubscriptionId);
#endif
        }

        /// <inheritdoc/>
        public override void Validate(ClusterDefinition clusterDefinition)
        {
            // Identify the OSD Bluestore block device for OSD nodes.

            if (cluster.Definition.Ceph.Enabled)
            {
                throw new NotImplementedException("$todo(jeff.lill): Implement this.");
            }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // Update the node labels with the actual capabilities of the 
            // virtual machines being provisioned.

            foreach (var node in cluster.Definition.Nodes)
            {
                var vmCaps = AzureVmCapabilities.Get(node.Azure.VmSize);

                node.Labels.ComputeCores = vmCaps.CoreCount;
                node.Labels.ComputeRamMB = vmCaps.RamSizeMB;

                if (node.Azure.HardDriveCount == 0)
                {
                    node.Labels.StorageCapacityGB = node.Azure.HardDriveSizeGB * node.Azure.HardDriveCount;
                    node.Labels.StorageSSD        = node.Azure.StorageType == AzureStorageTypes.PremiumLRS;
                    node.Labels.StorageLocal      = false;
                    node.Labels.StorageEphemeral  = false;
                    node.Labels.StorageRedundant  = true;
                }
                else
                {
                    node.Labels.StorageCapacityGB = vmCaps.EphemeralDriveGB;
                    node.Labels.StorageSSD        = vmCaps.EphemeralDriveSSD;
                    node.Labels.StorageLocal      = true;
                    node.Labels.StorageEphemeral  = true;
                    node.Labels.StorageRedundant  = false;
                }
            }

            // Connect to Azure.

            AzureConnect();

            // Assign IP addresses and frontend NAT ports for the cluster nodes
            // (including the pets).

            AssignVmAddresses();
            AssignVmNatPorts();

            var nextSshPort = azureOptions.FirstSshFrontendPort;

            foreach (var node in cluster.Managers.OrderBy(n => n.Name))
            {
                node.SshPort = nextSshPort++;
            }

            foreach (var node in cluster.Workers.OrderBy(n => n.Name))
            {
                node.SshPort = nextSshPort++;
            }

            foreach (var node in cluster.Pets.OrderBy(n => n.Name))
            {
                node.SshPort = nextSshPort++;
            }

            // Perform the provisioning operations.

            var operation  = $"Provisioning [{cluster.Definition.Name}] on Azure [{azureOptions.Region}/{resourceGroup}]";
            var controller = new SetupController<NodeDefinition>(operation, cluster.Nodes)
            {
                ShowStatus     = this.ShowStatus,
                ShowNodeStatus = false,
                MaxParallel    = this.MaxParallel
            };

            controller.AddGlobalStep("resource group", () => CreateResourceGroup());
            controller.AddGlobalStep("availability sets", () => CreateAvailabilitySets());
            controller.AddGlobalStep("network security groups", () => CreateNetworkSecurityGroups(tempSsh: true));
            controller.AddGlobalStep("virtual network", () => CreateVirtualNetwork());
            controller.AddGlobalStep("public addresses", () => CreatePublicAddresses());
            controller.AddGlobalStep("load balancers", () => CreateLoadbalancers(tempSsh: true));
            controller.AddGlobalStep("network interfaces", () => CreateNics());
            controller.AddGlobalStep("virtual machines", () => CreateVMs());
            controller.AddDelayStep($"cluster stabilize ({this.WaitSeconds}s)", TimeSpan.FromSeconds(this.WaitSeconds), "stabilize");

            if (!controller.Run(leaveNodesConnected: false))
            {
                Console.WriteLine("*** One or more Azure provisioning steps failed.");
                return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            var azureNode = nodeDictionary[nodeName];

            return (Address: azureNode.PublicSshAddress, Port: azureNode.PublicSshPort);
        }

        /// <inheritdoc/>
        public override string DrivePrefix
        {
            get { return "sd"; }
        }

        /// <summary>
        /// Assigns IP addresses from the cluster subnet to the virtual machines and/or 
        /// verifies that already assigned addresses are valid.
        /// </summary>
        private void AssignVmAddresses()
        {
            //-----------------------------------------------------------------
            // Assign IP addresses for the node primary NICs.

            // We're going to use a hash set to keep track of the addresses allocated
            // in the subnet.  The set is keyed on the address string representation.
            // We're going to preinitialize the set with the addresses reserved by Azure.
            //
            // Azure reserves the first 4 addresses along with the last address of the
            // subnet.

            var allocatedAddresses = new HashSet<string>();
            var subnet             = NetworkCidr.Parse(networkOptions.NodesSubnet);

            for (int i = 0; i < 4; i++)
            {
                allocatedAddresses.Add(NetHelper.AddressIncrement(subnet.Address, i).ToString());
            }

            allocatedAddresses.Add(NetHelper.AddressIncrement(subnet.Address, (int)(subnet.AddressCount - 1)).ToString());

            // Verify and reserve IP addresses for node definitions that specify explicit IP addresses.

            foreach (var node in cluster.Definition.Nodes.Where(n => !string.IsNullOrEmpty(n.PrivateAddress)))
            {
                var nodeAddress = IPAddress.Parse(node.PrivateAddress);

                if (!subnet.Contains(nodeAddress))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] reserves IP address [{node.PrivateAddress}] which is not within the [{subnet}] subnet assigned to the cluster.");
                }

                if (allocatedAddresses.Contains(nodeAddress.ToString()))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] reserves IP address [{node.PrivateAddress}] which conflicts with reserved Azure addresses (the first 4 and last addresses in the [{subnet}] subnet) or with another node.");
                }

                allocatedAddresses.Add(nodeAddress.ToString());
            }

            // Assign IP addresses to the remaining nodes in sorted order (by name).

            var nextAddress = subnet.Address;
            var lastAddress = NetHelper.AddressIncrement(subnet.Mask, (int)subnet.AddressCount - 1);

            foreach (var node in cluster.Definition.Nodes
                .Where(n => string.IsNullOrEmpty(n.PrivateAddress))
                .OrderBy(n => n.Name))
            {
                // Ignore any allocated addresses.

                while (true)
                {
                    if (NetHelper.AddressEquals(nextAddress, lastAddress))
                    {
                        throw new ClusterDefinitionException($"Cannot fit [{cluster.Definition.Nodes.Count()}] nodes into the [{subnet}] subnet on Azure.  You need to expand the subnet.");
                    }

                    if (!allocatedAddresses.Contains(nextAddress.ToString()))
                    {
                        break;
                    }

                    nextAddress = NetHelper.AddressIncrement(nextAddress);
                }

                node.PrivateAddress                       = nextAddress.ToString();
                cluster.GetNode(node.Name).PrivateAddress = nextAddress;   // We need to update this too.

                nextAddress  = NetHelper.AddressIncrement(nextAddress);
            }

            //-----------------------------------------------------------------
            // Assign IP addresses for the manager VPN server NICs.

            var vpnServerSubnet = NetworkCidr.Parse(networkOptions.CloudVpnSubnet);

            nextAddress = NetHelper.AddressIncrement(vpnServerSubnet.Address, 4);

            foreach (var node in cluster.Definition.Nodes.Where(n => n.IsManager).OrderBy(n => n.Name))
            {
                node.VpnPoolAddress = nextAddress.ToString();
                nextAddress         = NetHelper.AddressIncrement(nextAddress);
            }
        }

        /// <summary>
        /// Assign frontend NAT ports to the nodes.
        /// </summary>
        private void AssignVmNatPorts()
        {            
            // We need to assign a forwarding rule for each manager node to 
            // forward OpenVPN traffic to each manager.

            var nextVpnFrontendPort = azureOptions.FirstVpnFrontendPort;

            foreach (var manager in cluster.Managers.OrderBy(n => n.Name))
            {
                manager.Metadata.VpnFrontendPort = nextVpnFrontendPort++;
            }

            // Manager node SSH ports.

            var nextSshFrontendPort = azureOptions.FirstSshFrontendPort;

            foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsManager).OrderBy(n => n.Name))
            {
                if (azureOptions.PublicNodeAddresses)
                {
                    azureNode.PublicSshPort = NetworkPorts.SSH;
                }
                else
                {
                    azureNode.PublicSshPort = nextSshFrontendPort++;
                }
            }

            // Worker node SSH ports.

            foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsWorker).OrderBy(n => n.Name))
            {
                if (azureOptions.PublicNodeAddresses)
                {
                    azureNode.PublicSshPort = NetworkPorts.SSH;
                }
                else
                {
                    azureNode.PublicSshPort = nextSshFrontendPort++;
                }
            }

            // Pet node SSH ports.

            foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsPet).OrderBy(n => n.Name))
            {
                if (azureOptions.PublicNodeAddresses)
                {
                    azureNode.PublicSshPort = NetworkPorts.SSH;
                }
                else
                {
                    azureNode.PublicSshPort = nextSshFrontendPort++;
                }
            }
        }

        /// <summary>
        /// Initializes the resource group.
        /// </summary>
        private void CreateResourceGroup()
        {
#if AZURE_STUBBED
            if (azure.ResourceGroups.CheckExistence(resourceGroup))
            {
                return;
            }

            azure.ResourceGroups
                .Define(resourceGroup)
                .WithRegion(azureOptions.Region)
                .Create();
#endif
        }

        /// <summary>
        /// Creates the public IP addresses.
        /// </summary>
        private void CreatePublicAddresses()
        {
            //-----------------------------------------------------------------
            // Create a public address for the cluster load balancers.

            if (azureOptions.StaticClusterAddress)
            {
                pipLbManager = azure.PublicIPAddresses
                    .Define(pipLbManagerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithStaticIP()
                    .WithLeafDomainLabel(managerLeafDomainPrefix + azureOptions.DomainLabel)
                    .Create();

                pipLbWorker = azure.PublicIPAddresses
                    .Define(pipLbWorkerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithStaticIP()
                    .WithLeafDomainLabel(azureOptions.DomainLabel)
                    .Create();
            }
            else
            {
                pipLbManager = azure.PublicIPAddresses
                    .Define(pipLbManagerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithDynamicIP()
                    .WithLeafDomainLabel(managerLeafDomainPrefix + azureOptions.DomainLabel)
                    .Create();

                pipLbWorker = azure.PublicIPAddresses
                    .Define(pipLbWorkerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithDynamicIP()
                    .WithLeafDomainLabel(azureOptions.DomainLabel)
                    .Create();
            }

            cluster.Definition.Network.ManagerPublicAddress = pipLbManager.Fqdn;
            cluster.Definition.Network.WorkerPublicAddress  = pipLbWorker.Fqdn;

            //-----------------------------------------------------------------
            // Create dynamic public addresses for individual node VMs if requested.
            //
            // Note that we're going to use the combination of the node name
            // and the cluster's domain label label so each node will be unique
            // within an Azure region.

            var publicNamePrefix = $"{clusterName}-pip-vm-";

            if (azureOptions.PublicNodeAddresses)
            {
                var nodePipCreators = new List<Microsoft.Azure.Management.Network.Fluent.PublicIPAddress.Definition.IWithCreate>();

                foreach (var azureNode in nodeDictionary.Values)
                {
                    var pipCreator = azure.PublicIPAddresses
                        .Define($"{publicNamePrefix}{azureNode.Name}")
                        .WithRegion(azureOptions.Region)
                        .WithExistingResourceGroup(resourceGroup)
                        .WithDynamicIP()
                        .WithLeafDomainLabel($"{azureNode.Name}-{azureOptions.DomainLabel}");

                    nodePipCreators.Add(pipCreator);
                }

                var nodePips = azure.PublicIPAddresses.Create(nodePipCreators.ToArray());

                foreach (var pip in nodePips)
                {
                    var azureNode = nodeDictionary[pip.Name.Substring(publicNamePrefix.Length)];

                    azureNode.PublicAddress               = pip;
                    azureNode.PublicSshAddress            = 
                    azureNode.Node.Metadata.PublicAddress = pip.Fqdn;
                }
            }
            else
            {
                // The nodes don't have public IP addresses so we need to set the public 
                // SSH address of each node to the load balancer's public address.

                foreach (var azureNode in nodeDictionary.Values)
                {
                    azureNode.PublicSshAddress = azureNode.IsManager ? pipLbManager.Fqdn : pipLbWorker.Fqdn;
                }
            }
        }

        /// <summary>
        /// Creates the cluster availability set.
        /// </summary>
        private void CreateAvailabilitySets()
        {
            asetManager = azure.AvailabilitySets
                .Define(asetManagerName)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithSku(AvailabilitySetSkuTypes.Managed)
                .WithFaultDomainCount(2)
                .WithUpdateDomainCount(5)
                .Create();

            asetWorker = azure.AvailabilitySets
                .Define(asetWorkerName)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithSku(AvailabilitySetSkuTypes.Managed)
                .WithFaultDomainCount(azureOptions.FaultDomains)
                .WithUpdateDomainCount(azureOptions.UpdateDomains)
                .Create();
        }

        /// <summary>
        /// Creates the cluster's network security groups.
        /// </summary>
        /// <param name="updateNetworks">Optionally specifies which sets of load balancer and network security rules to be modified.</param>
        /// <param name="endpoints">Optional public cluster endpoints.</param>
        /// <param name="tempSsh">Optionally indicates  whether temporary rules should enable SSH traffic.</param>
        private void CreateNetworkSecurityGroups(NetworkUpdateSets updateNetworks = NetworkUpdateSets.All, List<HostedEndpoint> endpoints = null, bool tempSsh = false)
        {
            int priority;

            endpoints = endpoints ?? new List<HostedEndpoint>();

            if ((updateNetworks & NetworkUpdateSets.Manager) != 0)
            {
                //-----------------------------------------------------------------
                // Create the manager node security group.

                var nsgVpnCreator = azure.NetworkSecurityGroups
                    .Define(nsgVpnName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup);

                priority = 3000;

                // Allow inbound traffic from the nodes subnet.

                nsgVpnCreator
                    .DefineRule($"neon-AllowNodesInbound")
                    .AllowInbound()
                    .FromAddress(networkOptions.NodesSubnet)
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToAnyPort()
                    .WithAnyProtocol()
                    .WithPriority(priority++)
                    .Attach();

                if (tempSsh)
                {
                    // Temporarily allow inbound traffic to port SSH.  We'll delete this
                    // rule after the cluster has been provisioned.

                    priority = 4000;

                    nsgVpnCreator
                        .DefineRule($"neon-AllowInbound-{NetworkPorts.SSH}")
                        .AllowInbound()
                        .FromAnyAddress()
                        .FromAnyPort()
                        .ToAnyAddress()
                        .ToPort(NetworkPorts.SSH)
                        .WithAnyProtocol()
                        .WithPriority(priority++)
                        .Attach();
                }

                // Allow outbound traffic to everywhere.

                nsgVpnCreator
                    .DefineRule("neon-AllowAllOutbound")
                    .AllowOutbound()
                    .FromAnyAddress()
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToAnyPort()
                    .WithAnyProtocol()
                    .WithPriority(3000)
                    .Attach();

                nsgVpn = nsgVpnCreator.Create();
            }

            if ((updateNetworks & NetworkUpdateSets.Worker) != 0)
            {
                //-----------------------------------------------------------------
                // Create the worker node security group.

                var nsgNodeCreator = azure.NetworkSecurityGroups
                .Define(nsgNodeName)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup);

                // Allow inbound traffic for each of the public cluster endpoints.

                priority = 3000;

                foreach (var endpoint in endpoints.OrderBy(ep => ep.BackendPort))
                {
                    nsgNodeCreator
                        .DefineRule($"neon-AllowClusterEndpoint-{endpoint.FrontendPort}")
                        .AllowInbound()
                        .FromAnyAddress()
                        .FromAnyPort()
                        .ToAnyAddress()
                        .ToPort(endpoint.BackendPort)
                        .WithAnyProtocol()
                        .WithPriority(priority++)
                        .Attach();
                }

                // Allow OpenVPN traffic from the load balancer.

                nsgNodeCreator
                    .DefineRule($"neon-AllowOpenVPN-{NetworkPorts.OpenVPN}")
                    .AllowInbound()
                    .FromAnyAddress()
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToPort(NetworkPorts.OpenVPN)
                    .WithAnyProtocol()
                    .WithPriority(priority++)
                    .Attach();

                // Temporarily allow inbound traffic to port SSH.  We'll delete this
                // rule after the cluster has been provisioned.

                priority = 4000;

                nsgNodeCreator
                    .DefineRule($"neon-AllowInbound-{NetworkPorts.SSH}")
                    .AllowInbound()
                    .FromAnyAddress()
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToPort(NetworkPorts.SSH)
                    .WithAnyProtocol()
                    .WithPriority(priority++)
                    .Attach();

                // Allow outbound traffic to everywhere.

                nsgNodeCreator
                    .DefineRule("neon-AllowAllOutbound")
                    .AllowOutbound()
                    .FromAnyAddress()
                    .FromAnyPort()
                    .ToAnyAddress()
                    .ToAnyPort()
                    .WithAnyProtocol()
                    .WithPriority(3000)
                    .Attach();

                nsgNode = nsgNodeCreator.Create();
            }
        }

        /// <summary>
        /// Creates the virtual network.
        /// </summary>
        private void CreateVirtualNetwork()
        {
            // Create the routing table that will be used to route VPN return traffic
            // from the primary subnet back to the specific manager node that will
            // be able to route the packet packets back to the connected VPN client.

            var vpnRoutesDef = azure.Networks.Manager.RouteTables
                .Define(vpnRouteName)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup);

            foreach (var manager in nodeDictionary.Values.Where(n => n.IsManager).OrderBy(n => n.Name))
            {
                vpnRoutesDef
                    .DefineRoute($"vpn-return-via-{manager.Name}")
                    .WithDestinationAddressPrefix(manager.Node.Metadata.VpnPoolSubnet)
                    .WithNextHopToVirtualAppliance(manager.Node.Metadata.VpnPoolAddress)
                    .Attach();
            }

            var vpnRoutes = vpnRoutesDef.Create();

            // Create the virtual network with two subnets:
            //
            //      nodes (with the vpn return routes)
            //      vpn

            var vnetCreator = azure.Networks
                .Define(vnetName)
                .WithRegion(azureOptions.Region)
                .WithExistingResourceGroup(resourceGroup)
                .WithAddressSpace(NetworkCidr.Normalize(networkOptions.CloudVNetSubnet))
                .DefineSubnet(subnetNodesName)
                    .WithAddressPrefix(NetworkCidr.Normalize(networkOptions.NodesSubnet))
                    .WithExistingRouteTable(vpnRoutes.Id)
                    .Attach()
                .DefineSubnet(subnetVpnName)
                    .WithAddressPrefix(NetworkCidr.Normalize(networkOptions.CloudVpnSubnet))
                    .Attach();

            vnet = vnetCreator.Create();
        }

        /// <summary>
        /// Creates the cluster load balancer.
        /// </summary>
        /// <param name="updateNetworks">Optionally specifies which sets of load balancer and network security rules to be modified.</param>
        /// <param name="endpoints">Optional public cluster endpoints.</param>
        /// <param name="tempSsh">Optionally indicates whether temporary SSH forwarding rules should be included.</param>
        /// <remarks>
        /// <note>
        /// Temporary SSH port forwarding rules are never created if the cluster is configured
        /// to provision public IP addresses for each cluster node.
        /// </note>
        /// </remarks>
        private void CreateLoadbalancers(NetworkUpdateSets updateNetworks = NetworkUpdateSets.All, List<HostedEndpoint> endpoints = null, bool tempSsh = false)
        {
#if AZURE_STUBBED
            tempSsh   = tempSsh && !cluster.Definition.Hosting.Azure.PublicNodeAddresses;
            endpoints = endpoints ?? new List<HostedEndpoint>();

            if ((updateNetworks & NetworkUpdateSets.Manager) != 0)
            {
                //-----------------------------------------------------------------
                // Create the load balancer for the manager nodes.

                var lbDefManager = azure.LoadBalancers
                    .Define(lbManagerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .DefinePublicFrontend(feConfigName)
                        .WithExistingPublicIPAddress(pipLbManager)
                        .Attach()
                    .DefineBackend(bePoolName)
                        .Attach()

                    // Dummy health probe (see note below).

                    .DefineTcpProbe(probeName)
                        .WithPort(NeonHostPorts.ReservedUnused)
                        .WithIntervalInSeconds(60)
                        .WithNumberOfProbes(2)
                        .Attach();

                // $note(jeff.lill):
                //
                // My original plan was to load balance VPN traffic across the
                // manager nodes but I couldn't get this to work.  The symptoms
                // were:
                //
                //  * The load balancer would forward traffic to 1194 correctly
                //    after the manager nodes were first provisioned.
                //
                //  * But this would stop working after the manager was rebooted.
                //
                //  * Using [tcpdump] on the manager, I could see SYN packets
                //    coming from the load balancer to establish the TCP health
                //    probe connection, but I was seeing no ACK packets from
                //    OpenVPN.
                //
                //  * I tried establishing a TCP connection from one of the worker
                //    nodes and was seeing full connections being made.
                //
                //  * I tried setting up a simple web server on another port (77)
                //    on the manager and setting that as the health probe endpoint
                //    and saw the same behavior: SYN packets but no ACKs.
                //
                //  * Load balancing works fine for the worker nodes and its
                //    load balancer.
                //
                // It seems like this might have something to do with the fact
                // that manager nodes have the two NICs and we have the VPN
                // return route configured, but it seems like we'd still see the
                // ACK packets being generated on the manager in that case and
                // then have them being eaten up by the Azure gateway/router.
                // I can't figure this out so I'm going to move on.
                //
                // Forwarded ports do work, so we're going to go with that.

                // $todo(jeff.lill):
                // 
                // The fluent API currently requires at least one load balancing
                // rule to be defined.  I reported this and Microsoft is going
                // to refactor the API:
                //
                //      https://github.com/Azure/azure-sdk-for-net/issues/3173
                //
                // As a workaround, I'm going to load balance to a reserved unused
                // port.  Note that this port will be explicitly blocked by a
                // VPN network security group rule to prevent the odd security 
                // breach.

                var lbManagerCreator = lbDefManager
                    .DefineLoadBalancingRule($"neon-unused-tcp-{NeonHostPorts.ReservedUnused}")
                    .WithProtocol(TransportProtocol.Tcp)
                    .WithFrontend(feConfigName)
                    .WithFrontendPort(NeonHostPorts.ReservedUnused)
                    .WithProbe(probeName)
                    .WithBackend(bePoolName)
                    .WithIdleTimeoutInMinutes(5)
                    .Attach();

                foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsManager))
                {
                    lbManagerCreator
                        .DefineInboundNatRule(azureNode.VpnNatRuleName)
                        .WithProtocol(TransportProtocol.Tcp)
                        .WithFrontend(feConfigName)
                        .WithFrontendPort(azureNode.Node.Metadata.VpnFrontendPort)
                        .WithBackendPort(NetworkPorts.OpenVPN)
                        .WithIdleTimeoutInMinutes(5)
                        .Attach();
                }

                if (tempSsh)
                {
                    foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsManager))
                    {
                        lbManagerCreator
                            .DefineInboundNatRule(azureNode.SshNatRuleName)
                            .WithProtocol(TransportProtocol.Tcp)
                            .WithFrontend(feConfigName)
                            .WithFrontendPort(azureNode.PublicSshPort)
                            .WithBackendPort(NetworkPorts.SSH)
                            .WithIdleTimeoutInMinutes(5)
                            .Attach();
                    }
                }

                lbManager = lbManagerCreator.Create();
            }

            if ((updateNetworks & NetworkUpdateSets.Worker) != 0)
            {
                //-----------------------------------------------------------------
                // Create the load balancer for the worker nodes.

                if (pipLbWorker == null)
                {
                    // We need to fetch the worker load balancer's public IP address
                    // if we don't already have it (e.g. when we're not in the process
                    // of configuring the cluster).

                    pipLbWorker = azure.PublicIPAddresses.GetByResourceGroup(resourceGroup, pipLbWorkerName);
                }

                var lbDefWorker = azure.LoadBalancers
                    .Define(lbWorkerName)
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .DefinePublicFrontend(feConfigName)
                        .WithExistingPublicIPAddress(pipLbWorker)
                        .Attach()
                    .DefineBackend(bePoolName)
                        .Attach()

                    // We only need one probe because the Docker ingress network
                    // and HAProxy handles internal cluster fail-over.

                    .DefineTcpProbe(probeName)
                        .WithPort(NeonHostPorts.ProxyPublicHttp)
                        .WithIntervalInSeconds(5)
                        .WithNumberOfProbes(2)
                        .Attach();

                // Add a load balancing rule for each public cluster endpoint.

                IWithLoadBalancingRuleOrCreate lbWorkerCreator = null;

                if (endpoints.Count > 0)
                {
                    foreach (var endpoint in endpoints)
                    {
                        lbWorkerCreator = lbDefWorker
                            .DefineLoadBalancingRule($"neon-endpoint-tcp-{endpoint.FrontendPort}")
                            .WithProtocol(TransportProtocol.Tcp)
                            .WithFrontend(feConfigName)
                            .WithFrontendPort(endpoint.FrontendPort)
                            .WithProbe(probeName)
                            .WithBackend(bePoolName)
                            .WithBackendPort(endpoint.BackendPort)
                            .Attach();
                    }
                }
                else
                {
                    // $todo(jeff.lill):
                    // 
                    // The fluent API currently requires at least one load balancing
                    // rule to be defined.  I reported this and Microsoft is going
                    // to refactor the API:
                    //
                    //      https://github.com/Azure/azure-sdk-for-net/issues/3173
                    //
                    // As a workaround, I'm going to load balance to a reserved unused
                    // port.  Note that this port will be explicitly blocked by a
                    // VPN network security group rule to prevent the odd security 
                    // breach.

                    lbWorkerCreator = lbDefWorker
                        .DefineLoadBalancingRule($"neon-unused-tcp-{NeonHostPorts.ReservedUnused}")
                        .WithProtocol(TransportProtocol.Tcp)
                        .WithFrontend(feConfigName)
                        .WithFrontendPort(NeonHostPorts.ReservedUnused)
                        .WithProbe(probeName)
                        .WithBackend(bePoolName)
                        .WithIdleTimeoutInMinutes(5)
                        .Attach();
                }

                if (tempSsh)
                {
                    foreach (var azureNode in nodeDictionary.Values.Where(n => n.IsWorker))
                    {
                        lbWorkerCreator
                            .DefineInboundNatRule(azureNode.SshNatRuleName)
                            .WithProtocol(TransportProtocol.Tcp)
                            .WithFrontend(feConfigName)
                            .WithFrontendPort(azureNode.PublicSshPort)
                            .WithBackendPort(NetworkPorts.SSH)
                            .Attach();
                    }
                }

                lbWorker = lbWorkerCreator.Create();
            }
#endif
        }

        /// <summary>
        /// Creates the virtual network interfaces for each node.
        /// </summary>
        private void CreateNics()
        {
            var nodeNicCreators   = new List<Microsoft.Azure.Management.Network.Fluent.NetworkInterface.Definition.IWithCreate>();
            var nodeNicNamePrefix = $"{clusterName}-nic-node-";
            var vpnNicCreators    = new List<Microsoft.Azure.Management.Network.Fluent.NetworkInterface.Definition.IWithCreate>();
            var vpnNicNamePrefix  = $"{clusterName}-nic-vpn-";

            foreach (var azureNode in nodeDictionary.Values)
            {
                // All nodes need a NIC in the [nodes] subnet.  This will be the primary
                // (and only NIC) for workers and also the primary NIC for managers.

                var nodeNicCreator = azure.NetworkInterfaces
                    .Define($"{nodeNicNamePrefix}{azureNode.Node.Name}")
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithExistingPrimaryNetwork(vnet)
                    .WithSubnet(subnetNodesName)
                    .WithPrimaryPrivateIPAddressStatic(azureNode.Node.Metadata.PrivateAddress);

                if (azureOptions.PublicNodeAddresses)
                {
                    nodeNicCreator.WithExistingPrimaryPublicIPAddress(azureNode.PublicAddress);
                }

                // NICs in [nodes] subnet need to enable IP forwarding so they'll be
                // able to route VPN client return packets to the OpenVPN servers 
                // running as Virtual Appliances on the manager nodes.

                nodeNicCreator.WithIPForwarding();

                // Assign the security groups and load balancing parameters.

                if (azureNode.IsManager)
                {
                    nodeNicCreator
                        .WithExistingNetworkSecurityGroup(nsgNode)
                        .WithExistingLoadBalancerBackend(lbManager, bePoolName)
                        .WithExistingLoadBalancerInboundNatRule(lbManager, azureNode.SshNatRuleName)
                        .WithExistingLoadBalancerInboundNatRule(lbManager, azureNode.VpnNatRuleName);
                }
                else
                {
                    nodeNicCreator
                        .WithExistingNetworkSecurityGroup(nsgNode)
                        .WithExistingLoadBalancerBackend(lbWorker, bePoolName)
                        .WithExistingLoadBalancerInboundNatRule(lbWorker, azureNode.SshNatRuleName);
                }

                nodeNicCreators.Add(nodeNicCreator);

                // Manager nodes need their secondary NIC to be in the [vpn] subnet.

                if (azureNode.IsManager)
                {
                    var vpnServerNicCreator =
                        azure.NetworkInterfaces
                            .Define($"{vpnNicNamePrefix}{azureNode.Node.Name}")
                            .WithRegion(azureOptions.Region)
                            .WithExistingResourceGroup(resourceGroup)
                            .WithExistingPrimaryNetwork(vnet)
                            .WithSubnet(subnetVpnName)
                            .WithPrimaryPrivateIPAddressStatic(azureNode.Node.Metadata.VpnPoolAddress);

                    vpnServerNicCreator
                        .WithExistingNetworkSecurityGroup(nsgVpn);

                    vpnNicCreators.Add(vpnServerNicCreator);
                }
            }

            // Create the node NICs and associate each with the related node.

            var nodeNics = azure.NetworkInterfaces.Create(nodeNicCreators.ToArray());

            foreach (var nic in nodeNics)
            {
                var nodeName = nic.Name.Substring(nodeNicNamePrefix.Length);

                nodeDictionary[nodeName].NodesNic = nic;
            }

            // Create the VPN server NICs and associate each with the related manager nodes.

            var vpnNics = azure.NetworkInterfaces.Create(vpnNicCreators.ToArray());

            foreach (var nic in vpnNics)
            {
                var nodeName    = nic.Name.Substring(vpnNicNamePrefix.Length);
                var managerNode = nodeDictionary[nodeName];

                Covenant.Assert(managerNode.IsManager);

                managerNode.VpnNic = nic;
            }
        }

        /// <summary>
        /// Creates the cluster virtual machines.
        /// </summary>
        private void CreateVMs()
        {
            var vmCreators = new List<Microsoft.Azure.Management.Compute.Fluent.VirtualMachine.Definition.IWithManagedCreate>();
            var vmPrefix   = $"{clusterName}-vm-";

            foreach (var azureNode in nodeDictionary.Values)
            {
                var azureNodeOptions   = azureNode.Node.Metadata.Azure;
                var storageAccountType = StorageAccountTypes.StandardLRS;

                if (azureNodeOptions.HardDriveCount > 0)
                {
                    switch (azureNodeOptions.StorageType)
                    {
                        case AzureStorageTypes.PremiumLRS:

                            storageAccountType = StorageAccountTypes.PremiumLRS;
                            break;

                        case AzureStorageTypes.StandardLRS:

                            storageAccountType = StorageAccountTypes.StandardLRS;
                            break;

                        default:

                            throw new NotImplementedException();
                    }
                }

                INetworkInterface primaryNic;
                INetworkInterface secondaryNic = null;

                if (azureNode.IsManager)
                {
                    primaryNic   = azureNode.NodesNic;
                    secondaryNic = azureNode.VpnNic;
                }
                else
                {
                    primaryNic = azureNode.NodesNic;
                }

                var vmCreator = azure.VirtualMachines
                    .Define($"{vmPrefix}{azureNode.Name}")
                    .WithRegion(azureOptions.Region)
                    .WithExistingResourceGroup(resourceGroup)
                    .WithExistingPrimaryNetworkInterface(primaryNic)
                    .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                    .WithRootUsername(this.HostUsername)
                    .WithRootPassword(this.HostPassword)
                    .WithComputerName("ubuntu")  // DO NOT CHANGE: Node setup scripts require this server name.
                    .WithDataDiskDefaultStorageAccountType(storageAccountType);

                if (secondaryNic != null)
                {
                    vmCreator.WithExistingSecondaryNetworkInterface(secondaryNic);
                }

                if (azureNodeOptions.HardDriveCount > 0)
                {
                    for (int lun = 1; lun <= azureNodeOptions.HardDriveCount; lun++)
                    {
                        vmCreator.WithNewDataDisk(AzureHelper.GetDiskSizeGB(azureNodeOptions.StorageType, azureNodeOptions.HardDriveSizeGB), lun, CachingTypes.None);
                    }
                }

                vmCreator
                    .WithSize(azureNode.Node.Metadata.Azure.VmSize.ToString())
                    .WithExistingAvailabilitySet(azureNode.IsManager ? asetManager : asetWorker);

                vmCreators.Add(vmCreator);
            }

            // Create the VMs.

            var vms = azure.VirtualMachines.Create(vmCreators.ToArray());

            foreach (var vm in vms)
            {
                var nodeName = vm.Name.Substring(vmPrefix.Length);

                nodeDictionary[nodeName].Vm = vm;
            }
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
            // Azure doesn't initialize the secondary NIC on the manager nodes
            // properly when the VM is created so we need to do this ourselves.

            const string interfaceConfigPath = "/etc/network/interfaces.d/50-cloud-init.cfg";

            controller.AddStep("secondary nic",
                (manager, stepDelay) =>
                {
                    var interfaceConfig = manager.DownloadText(interfaceConfigPath);

                    if (!interfaceConfig.Contains("auto eth1"))
                    {
                        // Secondary interface hasn't been initialized yet.

                        interfaceConfig +=
@"
auto eth1
iface eth1 inet dhcp
";
                        manager.UploadText(interfaceConfigPath, interfaceConfig);
                        manager.SudoCommand("systemctl restart networking");
                    }
                },
                node => node.Metadata.IsManager);
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController<NodeDefinition> controller)
        {
            // Remove all temporary rules (e.g. allowing SSH) from the VPN and node network security groups.
            // We won't need these after provisioning has been completed because we can use the OpenVPN to
            // access the nodes via their private IP addresses instead.
            //
            // We're also going to reset the node SSH ports back to the default (22) because we're deleting
            // the temporary forwarding rules and and since we'll access the nodes via the VPN going forward.

            controller.AddGlobalStep("network security",
                () =>
                {
                    // Update the VPN security groups so they don't include the
                    // temporary SSH rules.

                    CreateNetworkSecurityGroups(tempSsh: false);

                    // Update the load balancers so they don't include the SSH 
                    // port forwarding rules.

                    CreateLoadbalancers(tempSsh: false);

                    // Reset the node SSH ports to the default since we'll be able to
                    // access the nodes directly now via the VPN.

                    foreach (var node in cluster.Nodes)
                    {
                        node.SshPort = NetworkPorts.SSH;
                    }
                });
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => true;

        /// <inheritdoc/>
        public override List<HostedEndpoint> GetPublicEndpoints()
        {
            var endpoints = new List<HostedEndpoint>();

            AzureConnect();

            lbWorker = azure.LoadBalancers.GetByResourceGroup(resourceGroup, lbWorkerName);

            foreach (var item in lbWorker.LoadBalancingRules.Where(r => r.Key.StartsWith("neon-endpoint-")))
            {
                HostedEndpointProtocol protocol;

                var rule = item.Value;

                if (rule.Protocol == TransportProtocol.Tcp)
                {
                    protocol = HostedEndpointProtocol.Tcp;
                }
                else if (rule.Protocol == TransportProtocol.Udp)
                {
                    protocol = HostedEndpointProtocol.Udp;
                }
                else
                {
                    continue;   // Unknown protocol
                }

                endpoints.Add(new HostedEndpoint(protocol, rule.FrontendPort, rule.BackendPort));
            }

            return endpoints;
        }

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            AzureConnect();

            CreateNetworkSecurityGroups(NetworkUpdateSets.Worker, endpoints);
            CreateLoadbalancers(NetworkUpdateSets.Worker, endpoints);
        }
    }
}
