//-----------------------------------------------------------------------------
// FILE:	    AzureHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

namespace Neon.Kube
{
    /// <summary>
    /// Manages cluster provisioning on the Google Cloud Platform.
    /// </summary>
    /// <remarks>
    /// </remarks>
    [HostingProvider(HostingEnvironments.Azure)]
    public class AzureHostingManager : HostingManager
    {
        // IMPLEMENTATION NOTE:
        // --------------------
        // Here's the original issue covering Azure provisioning and along with 
        // some discussion about how neonKUBE thinks about cloud deployments:
        // 
        //      https://github.com/nforgeio/neonKUBE/issues/908
        //
        // The remainder of this note will outline how Azure provisioning works.
        //
        // A neonKUBE Azure cluster will require provisioning these things:
        //
        //      * VNET
        //      * VMs & Drives
        //      * Load balancer with public IP
        //
        // In the future, we'll relax the public load balancer requirement so
        // that virtual air-gapped clusters can be supported (more on that below).
        //
        // The VNET will be configured using the cluster definitions's [NetworkOptions]
        // and the node IP addresses will be automatically assigned by default
        // but this can be customized via the cluster definition when necessary.
        // The load balancer will be created using a public IP address with
        // NAT rules forwarding network traffic into the cluster.  These rules
        // are controlled by [NetworkOptions.IngressRoutes] in the cluster
        // definition.  The target nodes in the cluster are indicated by the
        // presence of a [neonkube.io/node.ingress=true] label which can be
        // set explicitly for each node or assigned via a [NetworkOptions.IngressNodeSelector]
        // label selector.  neonKUBE will use reasonable defaults when necessary.
        //
        // VMs are currently based on the Ubuntu-20.04 Server image provided by 
        // published to the marketplace by Canonical.  They publish Gen1 and Gen2
        // images.  I believe Gen2 images will work on Azure Gen1 & Gen2 instances
        // so our images will be Gen2 based as well.
        //
        // This hosting manager will support creating VMs from the base Canonical
        // image as well as from custom images published to the marketplace by
        // neonFORGE.  The custom images will be preprovisioned with all of the
        // software required, making cluster setup much faster and reliable.  The
        // Canonical based images will need lots of configuration before they can
        // be added to a cluster.  Note that the neonFORGE images are actually
        // created by starting with a Canonical image and doing most of a cluster
        // setup on that image, so we'll continue supporting the raw Canonical
        // images.
        //
        // We're also going to be supporting two different was of managing the
        // cluster deployment process.  The first approach will be to continue
        // controlling the process from a client application: [neon-cli] or
        // neonDESKTOP using SSH to connect to the nodes via temporary NAT
        // routes through the public load balancer.  neonKUBE clusters reserve
        // 1000 inbound ports (the actual range is configurable in the cluster
        // definition [CloudOptions]) and we'll automatically create NAT rule
        // for each node that routes external SSH traffic to the node.
        //
        // The second approach is to handle cluster setup from within the cloud
        // itself.  We're probably going to defer doing until after we go public
        // with neonCLOUD.  There's two ways of accomplising this: one is to
        // deploy a very small temporary VM within the customer's Azure subscription
        // that lives within the cluster VNET and coordinates things from there.
        // The other way is to is to manage VM setup from a neonCLOUD service,
        // probably using temporary load balancer SSH routes to access specific
        // nodes.  Note that this neonCLOUD service could run anywhere; it is
        // not restricted to running withing the same region as the customer
        // cluster.
        // 
        // Node instance and disk types and sizes are specified by the 
        // [NodeDefinition.Azure] property.  Instance types are specified
        // using standard Azure names, disk type is an enum and disk sizes
        // are specified via strings including optional [ByteUnits].  Provisioning
        // will need to verify that the requested instance and drive types are
        // actually available in the target Azure region and will also need
        // to map the disk size specified by the user to the closest matching
        // Azure disk size.
        //
        // neonKUBE will allow zero or more Azure drives to be attached to
        // a cluster node.  Nodes with zero attached drives will be created
        // will have only a limited amount of disk space available.  The OS
        // drive in this case will actually be backed implicitly by an Azure
        // drive so data there will remain after any VM maintence operations
        // performed by Azure.
        //
        // Azure VMs are also provided with ephemeral disk space local to the VM 
        // itself.  On neonKUBE cluster Linux VMs, the ephemeral block device will
        // be [/dev/sdb] but we don't currently do anything with this (like
        // create and mount a file system).
        //
        // More than one Azure drive can be mounted to a VM and the drives will
        // implicitly have the same size.  neonKUBE will configure these drives
        // as a large RAID0 striped array favoring capacity and performance over
        // reliability.  Azure says that the chance of a drive failure is between
        // 0.1-0.2% per year so for a node with 4 RAID0 drives, there's may be
        // a 1/125 chance per year of losing a one of the drives in the VM 
        // resulting in complete data loss which isn't too bad, especially for
        // situations where a redundant data store is deployed across multiple
        // nodes in the cluster.
        //
        // neonKUBE may support combining multiple Azure drives in to a redundant
        // RAID5 configuration in the future to dramatically lower the possible
        // failure risk.  This happens after provisioning so we'll be able to
        // support this for all clouds.

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Ensures that the assembly hosting this hosting manager is loaded.
        /// </summary>
        public static void Load()
        {
            // We don't have to do anything here because the assembly is loaded
            // as a byproduct of calling this method.
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterProxy    cluster;
        private KubeSetupInfo   setupInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster being managed.</param>
        /// <param name="setupInfo">Specifies the cluster setup information.</param>
        /// <param name="logFolder">
        /// The folder where log files are to be written, otherwise or <c>null</c> or 
        /// empty if logging is disabled.
        /// </param>
        public AzureHostingManager(ClusterProxy cluster, KubeSetupInfo setupInfo, string logFolder = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null, nameof(cluster));
            Covenant.Requires<ArgumentNullException>(setupInfo != null, nameof(setupInfo));

            cluster.HostingManager = this;

            this.cluster   = cluster;
            this.setupInfo = setupInfo;
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
        public override void Validate(ClusterDefinition clusterDefinition)
        {
        }

        /// <inheritdoc/>
        public override bool Provision(bool force, string secureSshPassword, string orgSshPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secureSshPassword));

            throw new NotImplementedException("$todo(jefflill): Implement this.");
        }

        /// <inheritdoc/>
        public override (string Address, int Port) GetSshEndpoint(string nodeName)
        {
            return (Address: cluster.GetNode(nodeName).PrivateAddress.ToString(), Port: NetworkPorts.SSH);
        }

        /// <inheritdoc/>
        public override bool RequiresAdminPrivileges => true;
    }
}
