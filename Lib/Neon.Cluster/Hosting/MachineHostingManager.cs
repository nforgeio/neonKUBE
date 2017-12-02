//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Cluster
{
    /// <summary>
    /// Manages cluster provisioning directly on bare metal or virtual machines.
    /// </summary>
    public partial class MachineHostingManager : HostingManager
    {
        private ClusterProxy        cluster;
        private SetupController     controller;
        private bool                forceVmOverwrite;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        public MachineHostingManager(ClusterProxy cluster)
        {
            cluster.HostingManager = this;

            this.cluster = cluster;
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
            get { return !cluster.Definition.Hosting.Machine.DeployVMs; }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            this.forceVmOverwrite = force;

            if (IsProvisionNOP)
            {
                // There's nothing to do here.

                return true;
            }

            // If a public address isn't explicitly specified, we'll assume that the
            // tool is running inside the network and can access the private address.

            foreach (var node in cluster.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.PublicAddress))
                {
                    node.PublicAddress = node.PrivateAddress;
                }
            }

            // Initialize and perform the setup operations.

            controller = new SetupController($"Provisioning [{cluster.Definition.Name}] cluster", cluster.Nodes)
            {
                ShowStatus  = this.ShowStatus,
                MaxParallel = 1     // We're only going to prepare one VM at a time.
            };

            if (NeonHelper.IsWindows)
            {
                controller.AddGlobalStep("prepare hypervisor", () => PrepareHyperV());
                controller.AddStep("create virtual machines", n => ProvisionHyperVMachine(n));
                controller.AddGlobalStep(string.Empty, () => FinishHyperV(), quiet: true);
            }
            else if (NeonHelper.IsOSX)
            {
                controller.AddGlobalStep("prepare hypervisor", () => PrepareVirtualBox());
                controller.AddStep("create virtual machines", n => ProvisionVirtualBoxMachine(n));
                controller.AddGlobalStep(string.Empty, () => FinishVirtualBox(), quiet: true);
            }
            else
            {
                throw new NotSupportedException("neonCLUSTER virtual machines may only be deployed on Windows or Macintosh OSX.");
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Performs any required initialization before host nodes can be provisioned.
        /// </summary>
        /// <param name="force">Specifies whether any existing named VMs are to be stopped and overwritten.</param>
        private void PrepareVirtualization(bool force)
        {
            if (NeonHelper.IsWindows)
            {
                PrepareHyperV();
            }
            else if (NeonHelper.IsOSX)
            {
                PrepareVirtualBox();
            }
            else
            {
                throw new NotSupportedException("neonCLUSTER virtual machines may only be deployed on Windows or Macintosh OSX.");
            }
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
