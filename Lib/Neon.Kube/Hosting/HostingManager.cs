//-----------------------------------------------------------------------------
// FILE:	    HostingManager.cs
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Base class for environment specific hosting managers. 
    /// </summary>
    public abstract class HostingManager : IHostingManager
    {
        /// <summary>
        /// Finalizer.
        /// </summary>
        ~HostingManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases any important resources associated with the instance.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if the instance is being disposed as opposed to being finalized.</param>
        public abstract void Dispose(bool disposing);

        /// <summary>
        /// Specifies whether the class should print setup status to the console.
        /// This defaults to <c>false</c>.
        /// </summary>
        public bool ShowStatus { get; set; } = false;

        /// <summary>
        /// The maximum number of nodes that will execute provisioning steps in parallel.  This
        /// defaults to <b>5</b>.
        /// </summary>
        public int MaxParallel { get; set; } = 5;

        /// <summary>
        /// Number of seconds to delay after specific operations (e.g. to allow services to stablize).
        /// This defaults to <b>0.0</b>.
        /// </summary>
        public double WaitSeconds { get; set; } = 0.0;

        /// <inheritdoc/>
        public virtual bool IsProvisionNOP
        {
            get { return false; }
        }

        /// <inheritdoc/>
        public abstract void Validate(ClusterDefinition clusterDefinition);

        /// <inheritdoc/>
        public virtual bool RequiresAdminPrivileges => true;

        /// <inheritdoc/>
        public virtual bool GenerateSecurePassword => true;

        /// <inheritdoc/>
        public abstract Task<bool> ProvisionAsync(ClusterLogin clusterLogin, string secureSshPassword, string orgSshPassword = null);

        /// <inheritdoc/>
        public virtual void AddPostProvisionSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public virtual bool CanManageRouter => false;

        /// <inheritdoc/>
        public virtual async Task UpdateInternetRoutingAsync()
        {
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task EnableInternetSshAsync()
        {
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task DisableInternetSshAsync()
        {
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public abstract (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <inheritdoc/>
        public abstract string GetDataDevice(SshProxy<NodeDefinition> node);

        /// <summary>
        /// Used by cloud and potentially other hosting manager implementations to verify the
        /// node address assignments and/or to automatically assign these addresses.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        protected void AssignNodeAddresses(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var networkOptions = clusterDefinition.Network;

            // Ensure that any explicit node IP address assignments are located
            // within the nodes subnet and do not conflict with any of the addresses
            // reserved by the cloud provider or neonKUBE.  We're also going to 
            // require that the nodes subnet have at least 256 addresses.

            var nodeSubnet = NetworkCidr.Parse(networkOptions.NodeSubnet);

            if (nodeSubnet.AddressCount < 256)
            {
                throw new ClusterDefinitionException($"[{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is too small.  Cloud subnets must include at least 256 addresses (at least a /24 network).");
            }

            const int reservedCount = KubeConst.CloudVNetStartReservedIPs + KubeConst.CloudVNetEndReservedIPs;

            if (clusterDefinition.Nodes.Count() > nodeSubnet.AddressCount - reservedCount)
            {
                throw new ClusterDefinitionException($"The cluster includes [{clusterDefinition.Nodes.Count()}] which will not fit within the [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] after accounting for [{reservedCount}] reserved addresses.");
            }

            var firstValidAddressUint = NetHelper.AddressToUint(nodeSubnet.FirstAddress) + KubeConst.CloudVNetStartReservedIPs;
            var firstValidAddress     = NetHelper.UintToAddress(firstValidAddressUint);
            var lastValidAddressUint  = NetHelper.AddressToUint(nodeSubnet.LastAddress) - KubeConst.CloudVNetEndReservedIPs;
            var lastValidAddress      = NetHelper.UintToAddress(lastValidAddressUint);

            foreach (var node in clusterDefinition.SortedNodes.OrderBy(node => node.Name))
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    // Ignore nodes with unassigned addresses.

                    continue;
                }

                var address = IPAddress.Parse(node.Address);

                if (!nodeSubnet.Contains(address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] is assigned [{node.Address}={node.Address}] which is outside of [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}].");
                }

                var addressUint = NetHelper.AddressToUint(address);

                if (addressUint < firstValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The first valid node address [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is [{firstValidAddress}].");
                }

                if (addressUint > lastValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The last valid node address [{nameof(networkOptions.NodeSubnet)}={networkOptions.NodeSubnet}] is [{lastValidAddress}].");
                }
            }

            //-----------------------------------------------------------------
            // Automatically assign unused IP addresses within the subnet to nodes that 
            // were not explicitly assigned an address in the cluster definition.

            // Add any explicitly assigned addresses to a HashSet so we won't reuse any.

            var assignedAddresses = new HashSet<uint>();

            foreach (var node in clusterDefinition.SortedNodes)
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                var address     = IPAddress.Parse(node.Address);
                var addressUint = NetHelper.AddressToUint(address);

                if (!assignedAddresses.Contains(addressUint))
                {
                    assignedAddresses.Add(addressUint);
                }
            }

            // Assign subnet addresses to the nodes, assigning master nodes first.

            foreach (var azureNode in clusterDefinition.SortedMasterThenWorkerNodes)
            {
                if (!string.IsNullOrEmpty(azureNode.Address))
                {
                    continue;
                }

                for (var addressUint = firstValidAddressUint; addressUint <= lastValidAddressUint; addressUint++)
                {
                    if (!assignedAddresses.Contains(addressUint))
                    {
                        azureNode.Address = NetHelper.UintToAddress(addressUint).ToString();

                        assignedAddresses.Add(addressUint);
                        break;
                    }
                }
            }
        }
    }
}
