//-----------------------------------------------------------------------------
// FILE:	    XenServerHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading;

using Newtonsoft.Json;

using Neon.Cluster.XenServer;
using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Manages cluster provisioning on the XenServer hypervisor.
    /// </summary>
    public partial class XenServerHostingManager : HostingManager
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to persist information about downloaded VHD template files.
        /// </summary>
        public class DriveTemplateInfo
        {
            /// <summary>
            /// The downloaded file ETAG.
            /// </summary>
            [JsonProperty(PropertyName = "ETag", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public string ETag { get; set; }

            /// <summary>
            /// The downloaded file length used as a quick verification that
            /// the complete file was downloaded.
            /// </summary>
            [JsonProperty(PropertyName = "Length", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(-1)]
            public long Length { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private ClusterProxy                cluster;
        private string                      logFolder;
        private List<XenClient>             xenHosts;
        private SetupController<XenClient>  controller;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public XenServerHostingManager(ClusterProxy cluster, string logFolder = null)
        {
            this.cluster                = cluster;
            this.cluster.HostingManager = this;
            this.logFolder              = logFolder;
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (xenHosts != null)
            {
                foreach (var xenHost in xenHosts)
                {
                    xenHost.Dispose();
                }

                xenHosts = null;
            }

            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        /// <inheritdoc/>
        public override bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // $todo(jeff.lill):
            //
            // I'm not implementing [force] here.  I'm not entirely sure
            // that this makes sense for production clusters and especially
            // when there are pet nodes.
            //
            // Perhaps it would make more sense to replace this with a
            // [neon cluster remove] command.
            //
            //      https://github.com/jefflill/NeonForge/issues/156

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // Build a list of [SshProxy] instances that map to the specified XenServer
            // hosts.  We'll use the [XenClient] instances as proxy metadata.

            var sshProxies = new List<SshProxy<XenClient>>();

            xenHosts = new List<XenClient>();

            foreach (var host in cluster.Definition.Hosting.VmHosts)
            {
                var hostAddress  = host.Address;
                var hostName     = host.Name;
                var hostUsername = host.Username ?? cluster.Definition.Hosting.VmHostUsername;
                var hostPassword = host.Password ?? cluster.Definition.Hosting.VmHostPassword;

                if (string.IsNullOrEmpty(hostName))
                {
                    hostName = host.Address;
                }

                var xenHost = new XenClient(hostAddress, hostUsername, hostPassword, name: host.Name, logFolder: logFolder);

                xenHosts.Add(xenHost);
                sshProxies.Add(xenHost.SshProxy);
            }

            // We're going to provision the XenServer hosts in parallel to
            // speed up cluster setup.  This works because each XenServer
            // is essentially independent from the others.

            controller = new SetupController<XenClient>($"Provisioning [{cluster.Definition.Name}] cluster", sshProxies)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = this.MaxParallel
            };

            controller.AddWaitUntilOnlineStep();
            controller.AddStep("verify readiness", sshProxy => VerifyReadiness(sshProxy));
            controller.AddStep("virtual machine template", sshProxy => CheckVmTemplate(sshProxy));
            controller.AddStep("provision virtual machines", sshProxy => ProvisionVirtualMachines(sshProxy));

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the list of <see cref="NodeDefinition"/> instances describing which cluster
        /// nodes are to be hosted by a specific XenServer.
        /// </summary>
        /// <param name="xenHost">The target XenServer.</param>
        /// <returns>The list of nodes to be hosted on the XenServer.</returns>
        private List<NodeDefinition> GetHostedNodes(XenClient xenHost)
        {
            var nodeDefinitions = cluster.Definition.NodeDefinitions.Values;

            return nodeDefinitions.Where(n => n.VmHost.Equals(xenHost.Name))
                .OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Returns the name to use for naming the virtual machine hosting the node.
        /// currently, this is the name of the cluster (lowercase) followed by a 
        /// dash and then the node name.  This convention will help disambiguate
        /// nodes from multiple clusters on the same hypervisor hosts.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The virtual machine name.</returns>
        private string GetVmName(NodeDefinition node)
        {
            return $"{cluster.Definition.Name.ToLowerInvariant()}-{node.Name}";
        }

        /// <summary>
        /// Verify that the XenServer is ready to provision the cluster virtual machines.
        /// </summary>
        /// <param name="sshProxy">The XenServer SSH proxy.</param>
        private void VerifyReadiness(SshProxy<XenClient> sshProxy)
        {
            // $todo(jeff.lill):
            //
            // It would be nice to verify that XenServer actually has enough 
            // resources (RAM, DISK, and perhaps CPU) here as well.

            var xenHost = sshProxy.Metadata;
            var nodes   = GetHostedNodes(xenHost);

            sshProxy.Status = "check virtual machines";

            var vmNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vm in xenHost.Machine.List())
            {
                vmNames.Add(vm.NameLabel);
            }

            foreach (var hostedNode in nodes)
            {
                var vmName = GetVmName(hostedNode);

                if (vmNames.Contains(vmName))
                {
                    sshProxy.Fault($"XenServer [{xenHost.Name}] is already hosting a virtual machine named [{vmNames}].");
                    return;
                }
            }
        }

        /// <summary>
        /// Install the virtual machine template on the XenServer if it's not already present.
        /// </summary>
        /// <param name="sshProxy">The XenServer SSH proxy.</param>
        private void CheckVmTemplate(SshProxy<XenClient> sshProxy)
        {
            var xenHost      = sshProxy.Metadata;
            var templateName = cluster.Definition.Hosting.XenServer.TemplateName;

            sshProxy.Status = "check VM template";

            if (xenHost.Template.Find(templateName) == null)
            {
                sshProxy.Status = "install VM template";
                xenHost.Template.Install(cluster.Definition.Hosting.XenServer.HostXvaUri, templateName);
            }
        }

        /// <summary>
        /// Provision the virtual machines on the XenServer.
        /// </summary>
        /// <param name="sshProxy">The XenServer SSH proxy.</param>
        private void ProvisionVirtualMachines(SshProxy<XenClient> sshProxy)
        {
            var xenHost = sshProxy.Metadata;

            foreach (var node in GetHostedNodes(xenHost))
            {
                var vmName      = GetVmName(node);
                var processors  = node.GetVmProcessors(cluster.Definition);
                var memoryBytes = node.GetVmMemory(cluster.Definition);
                var diskBytes   = node.GetVmDisk(cluster.Definition);

                sshProxy.Status = $"create: {vmName}";

                var vm = xenHost.Machine.Install(vmName, cluster.Definition.Hosting.XenServer.TemplateName,
                    processors: processors,
                    memoryBytes: memoryBytes,
                    diskBytes: diskBytes);

                sshProxy.Status = $"start: {vmName}";

                xenHost.Machine.Start(vm);

                // We need to wait for the virtual machine to start and obtain
                // and IP address via DHCP.

                string address;

                sshProxy.Status = $"get ip address";

                try
                {
                    NeonHelper.WaitFor(
                        () =>
                        {
                            while (true)
                            {
                                vm = xenHost.Machine.Find(vmName);

                                if (!string.IsNullOrEmpty(vm.Address))
                                {
                                    address = vm.Address;
                                    return true;
                                }

                                Thread.Sleep(1000);
                            }
                        },
                        TimeSpan.FromSeconds(120));
                }
                catch (TimeoutException)
                {
                    sshProxy.Fault("Timeout waiting for virtual machine to start and be assigned a DHCP address.");
                }

                // SSH into the VM using the DHCP address, configure the static IP
                // address and then reboot.

                var subnet    = NetworkCidr.Parse(cluster.Definition.Network.PremiseSubnet);
                var gateway   = cluster.Definition.Network.Gateway;
                var broadcast = cluster.Definition.Network.Broadcast;

                // We're going to temporarily set the node to the current VM address
                // so we can connect via SSH.

                using (var nodeProxy = cluster.GetNode(node.Name))
                {
                    sshProxy.Status = $"connecting to: {vmName}";
                    nodeProxy.Connect();

                    // Replace the [/etc/network/interfaces] file to configure the static
                    // IP and then reboot to reinitialize networking subsystem.

                    sshProxy.Status = $"set static ip [{node.PrivateAddress}]";

                    var interfacesText =
$@"# This file describes the network interfaces available on your system
# and how to activate them. For more information, see interfaces(5).

source /etc/network/interfaces.d/*

# The loopback network interface
auto lo
iface lo inet loopback

# The primary network interface
auto eth0
iface eth0 inet static
address {node.PrivateAddress}
netmask {subnet.Mask}
gateway {gateway}
broadcast {broadcast}
";
                    nodeProxy.UploadText("/etc/network/interfaces", interfacesText);

                    // Temporarily configure the public Google DNS servers as
                    // the name servers so DNS will work after we reboot with
                    // the static IP.  Note that cluster setup will eventually
                    // configure the name servers specified in the cluster
                    // definition.

                    // $todo(jeff.lill):
                    //
                    // Is there a good reason why we're not just configuring the
                    // DNS servers from the cluster definition here???
                    //
                    // Using the Google DNS seems like it could break some cluster
                    // network configurations (i.e. for clusters that don't have
                    // access to the public Internet).  Totally private clusters
                    // aren't really a supported scenario right now though because
                    // we assume we can use [apt-get]... to pull down packages.

                    var resolvBaseText =
$@"nameserver 8.8.8.8
nameserver 8.8.4.4
";
                    nodeProxy.UploadText("/etc/resolvconf/resolv.conf.d/base", resolvBaseText);

                    // Reboot to pick up the changes.

                    sshProxy.Status = $"rebooting";
                    nodeProxy.Reboot(wait: false);
                }
            }
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override List<HostedEndpoint> GetPublicEndpoints()
        {
            // Note that public endpoints have to be managed manually for
            // on-premise cluster deployments so we're going to return an 
            // empty list.

            return new List<HostedEndpoint>();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => false;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
            // Note that public endpoints have to be managed manually for
            // on-premise cluster deployments.
        }
    }
}
