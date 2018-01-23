//-----------------------------------------------------------------------------
// FILE:	    AwsHostingManager.cs
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
    /// Manages cluster provisioning on Amazon Web Services.
    /// </summary>
    public class AwsHostingManager : HostingManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AwsHostingManager(ClusterProxy cluster, string logFolder = null)
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
        public override bool Provision(bool force)
        {
            throw new NotImplementedException("$todo(jeff.lill): Implement this.");
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
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override bool CanUpdatePublicEndpoints => true;

        /// <inheritdoc/>
        public override void UpdatePublicEndpoints(List<HostedEndpoint> endpoints)
        {
        }
    }
}
