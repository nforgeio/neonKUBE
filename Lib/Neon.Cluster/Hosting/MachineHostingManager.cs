//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Cluster.HyperV;
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
    public class MachineHostingManager : HostingManager
    {
        private ClusterProxy cluster;

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
        public override bool Provision()
        {
            // If a public address isn't explicitly specified, we'll assume that the
            // tool is running inside the network and can access the private address.

            foreach (var node in cluster.Definition.Nodes)
            {
                if (string.IsNullOrEmpty(node.PublicAddress))
                {
                    node.PublicAddress = node.PrivateAddress;
                }
            }

            if (cluster.Definition.Hosting.Machine.DeployVMs)
            {
                DeployVMs();
            }

            return true;
        }

        /// <summary>
        /// Handles the deployment of the cluster virtual machines.
        /// </summary>
        private void DeployVMs()
        {
            if (NeonHelper.IsWindows)
            {
                DeployWindowsVMs();
            }
            else if (NeonHelper.IsOSX)
            {
                DeployOsxVMs();
            }
            else
            {
                throw new NotSupportedException("neonCLUSTER virtual machines may only be deployed on Windows or Macintosh OSX.");
            }
        }

        /// <summary>
        /// Handles the deploymenmt of the cluster virtual machines on 
        /// Windows Hyper-V.
        /// </summary>
        private void DeployWindowsVMs()
        {
            using (var hyperv = new HyperVClient())
            {
            }
        }

        /// <summary>
        /// Handles the deploymenmt of the cluster virtual machines on 
        /// Macintosh OSX.
        /// </summary>
        private void DeployOsxVMs()
        {
            // $todo(jeff.lill): Implement this.

            throw new NotImplementedException();
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
