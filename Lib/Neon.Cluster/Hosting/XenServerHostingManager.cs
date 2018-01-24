//-----------------------------------------------------------------------------
// FILE:	    XenServerHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cluster;
using Neon.Cluster.XenServer;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

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

            // $todo(jeff.lill): DELETE THIS --------------------------

            //using (var xenClient = new XenClient("10.50.0.217", "root", ""))
            //{
                //xenClient.Template.Destroy(xenClient.Template.Find("neon-template"));

                //var repos     = xenClient.StorageRepository.List();
                //var template  = xenClient.Template.Install("http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.xva", "neon-template");
                //var templates = xenClient.Template.List();

                //var vm = xenClient.VirtualMachine.Install("myVM", "neon-template", processors: 2, memoryBytes: NeonHelper.Giga, diskBytes: 25L * NeonHelper.Giga);

                //vm = xenClient.VirtualMachine.Find("myVM");

                //if (vm.IsRunning)
                //{
                //    xenClient.VirtualMachine.Shutdown(vm);
                //}

                //vm = xenClient.VirtualMachine.Find("myVM");

                //xenClient.VirtualMachine.Start(vm);

                //vm = xenClient.VirtualMachine.Find("myVM");

                //xenClient.VirtualMachine.Shutdown(vm);

                //vm = xenClient.VirtualMachine.Find("myVM");
            //}

            //---------------------------------------------------------
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

            controller.AddStep("connect", sshProxy => Connect(sshProxy));
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
                .OrderBy(n => n.Name)
                .ToList();
        }

        /// <summary>
        /// Connect to the XenServer.
        /// </summary>
        /// <param name="sshProxy">The XenServer SSH proxy.</param>
        private void Connect(SshProxy<XenClient> sshProxy)
        {
            sshProxy.Status = "connecting";
            sshProxy.Connect();
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

            sshProxy.Status = "checking virtual machine conflicts";

            var vmNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var vm in xenHost.VirtualMachine.List())
            {
                vmNames.Add(vm.NameLabel);
            }

            foreach (var hostedNode in nodes)
            {
                if (vmNames.Contains(hostedNode.Name))
                {
                    sshProxy.Fault($"XenServer [{xenHost.Name}] is already hosting a virtual machine named [{hostedNode.Name}].");
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

            sshProxy.Status = "check virtual machine template";

            if (xenHost.Template.Find(templateName) == null)
            {
                sshProxy.Status = "install virtual machine template";
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
                var processors  = node.GetVmProcessors(cluster.Definition);
                var memoryBytes = node.GetVmMemory(cluster.Definition);
                var diskBytes   = node.GetVmDisk(cluster.Definition);

                sshProxy.Status = $"creating: {node.Name}";

                var vm = xenHost.VirtualMachine.Install(node.Name, cluster.Definition.Hosting.XenServer.TemplateName,
                    processors: processors, 
                    memoryBytes: memoryBytes, 
                    diskBytes: diskBytes);

                sshProxy.Status = $"starting: {node.Name}";

                xenHost.VirtualMachine.Start(vm);
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
