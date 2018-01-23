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

        private ClusterProxy                            cluster;
        private SetupController                         controller;
        private Dictionary<string, NodeProxy<object>>   nameToXenProxy;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        public XenServerHostingManager(ClusterProxy cluster)
        {
            cluster.HostingManager = this;
            nameToXenProxy         = new Dictionary<string, NodeProxy<object>>();

            this.cluster = cluster;

            // $todo(jeff.lill): DELETE THIS --------------------------

            using (var xenClient = new XenClient("10.50.0.217", "root", ""))
            {
                //xenClient.Template.Destroy(xenClient.Template.Find("neon-template"));

                //var repos     = xenClient.StorageRepository.List();
                //var template  = xenClient.Template.Install("http://s3-us-west-2.amazonaws.com/neonforge/neoncluster/ubuntu-16.04.latest-prep.xva", "neon-template");
                //var templates = xenClient.Template.List();

                // var vm = xenClient.VirtualMachine.Install("myVM", "neon-template");

                var vm = xenClient.VirtualMachine.Find("myVM");

                if (vm.IsRunning)
                {
                    xenClient.VirtualMachine.Shutdown(vm);
                }

                vm = xenClient.VirtualMachine.Find("myVM");

                xenClient.VirtualMachine.Start(vm);

                vm = xenClient.VirtualMachine.Find("myVM");

                xenClient.VirtualMachine.Shutdown(vm);

                vm = xenClient.VirtualMachine.Find("myVM");
            }

            //---------------------------------------------------------
        }

        /// <inheritdoc/>
        public override void Dispose(bool disposing)
        {
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
            throw new NotImplementedException("$todo(jeff.lill): Need to complete this.");

            // If a public address isn't explicitly specified, we'll assume that the
            // tool is running inside the network and we can access the private address.

            foreach (var node in cluster.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.PublicAddress))
                {
                    node.PublicAddress = node.PrivateAddress;
                }
            }

            // Initialize and perform the setup operations.

            //controller = new SetupController($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            //{
            //    ShowStatus  = this.ShowStatus,
            //    MaxParallel = 1     // We're only going to prepare one VM at a time.
            //};

            //controller.AddGlobalStep("prepare hyper-v", () => PrepareXenServer());
            //controller.AddStep("create virtual machines", n => ProvisionVM(n));
            //controller.AddGlobalStep(string.Empty, () => FinishXenServer(), quiet: true);

            //if (!controller.Run())
            //{
            //    System.Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
            //    return false;
            //}

            return true;
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override void AddPostProvisionSteps(SetupController controller)
        {
        }

        /// <inheritdoc/>
        public override void AddPostVpnSteps(SetupController controller)
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
