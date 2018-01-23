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

        private ClusterProxy                    cluster;
        private SetupController<NodeDefinition> controller;
        private Dictionary<string, XenClient>   xenHosts;

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
            this.xenHosts               = new Dictionary<string, XenClient>();

            // $todo(jeff.lill): DELETE THIS --------------------------

            //using (var xenClient = new XenClient("10.50.0.217", "root", ""))
            //{
            //    xenClient.Template.Destroy(xenClient.Template.Find("neon-template"));

            //    var repos     = xenClient.StorageRepository.List();
            //    var template  = xenClient.Template.Install("http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.xva", "neon-template");
            //    var templates = xenClient.Template.List();

            //    var vm = xenClient.VirtualMachine.Install("myVM", "neon-template", memoryBytes: NeonHelper.Giga, diskBytes: 25L * NeonHelper.Giga);

            //    vm = xenClient.VirtualMachine.Find("myVM");

            //    if (vm.IsRunning)
            //    {
            //        xenClient.VirtualMachine.Shutdown(vm);
            //    }

            //    vm = xenClient.VirtualMachine.Find("myVM");

            //    xenClient.VirtualMachine.Start(vm);

            //    vm = xenClient.VirtualMachine.Find("myVM");

            //    xenClient.VirtualMachine.Shutdown(vm);

            //    vm = xenClient.VirtualMachine.Find("myVM");
            //}

            //---------------------------------------------------------
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
            if (xenHosts != null)
            {
                foreach (var xenHost in xenHosts.Values)
                {
                    xenHost.Dispose();
                }
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

            // We're going to provision the XenServer hosts in parallel to
            // speed up cluster setup.  This works because each XenServer
            // is essentially independent from the others.
            
            // Initialize and perform the provisioning operations.

            controller = new SetupController<NodeDefinition>($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = this.MaxParallel
            };

            //controller.AddStep("create virtual machines", n => ProvisionVM(n));

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
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
