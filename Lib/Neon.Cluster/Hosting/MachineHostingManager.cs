//-----------------------------------------------------------------------------
// FILE:	    MachineHostingManager.cs
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
            get { return true; }
        }

        /// <inheritdoc/>
        public override bool Provision(bool force)
        {
            // There's nothing to do here.

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
