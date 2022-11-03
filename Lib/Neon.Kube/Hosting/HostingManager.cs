//-----------------------------------------------------------------------------
// FILE:	    HostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Base class for environment specific hosting managers. 
    /// </summary>
    public abstract class HostingManager : IHostingManager
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Maximum number of async operations that hosting managers should perform
        /// in parallel.
        /// </summary>
        protected const int MaxAsyncParallelHostingOperations = 25;

        /// <summary>
        /// Determines whether the hosting environment supports <b>fstrim</b>.
        /// </summary>
        /// <param name="environment">Specifies the hosting environment.</param>
        /// <returns><c>true</c> if <b>fstrim</b> is supported.</returns>
        public static bool SupportsFsTrim(HostingEnvironment environment)
        {
            return environment != HostingEnvironment.Aws &&
                   environment != HostingEnvironment.XenServer;
        }

        /// <summary>
        /// Determines whether the hosting environment supports <b>zeroing</b>
        /// block devices.
        /// </summary>
        /// <param name="environment">Specifies the hosting environment.</param>
        /// <returns><c>true</c> if <b>fstrim</b> is supported.</returns>
        public static bool SupportsFsZero(HostingEnvironment environment)
        {
            // AWS EC2 backed block devices shouldn't be zeroed because that will
            // actually make snapshots and thus AMIs created from the snapshots
            // bigger and initially slower to boot.
            //
            // https://aws.amazon.com/blogs/apn/how-to-build-sparse-ebs-volumes-for-fun-and-easy-snapshotting/
            //
            // The same thing will happen on other cloud environments with sparse
            // page blobs, so we'll disable this for all clouds.

            return !KubeHelper.IsCloudEnvironment(environment);
        }

        //---------------------------------------------------------------------
        // Instance members

        private int? maxParallel = null;

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

        /// <inheritdoc/>
        public virtual int MaxParallel
        {
            get
            {
                if (!maxParallel.HasValue)
                {
                    maxParallel = KubeHelper.IsCloudEnvironment(HostingEnvironment) ? 100 : 25;
                }

                return maxParallel.Value;
            }

            set
            {
                Covenant.Requires<ArgumentException>(value > 0, nameof(MaxParallel));

                maxParallel = value;
            }
        }

        /// <inheritdoc/>
        public double WaitSeconds { get; set; } = 0.0;

        /// <inheritdoc/>
        public virtual int NodeMtu => 0;

        /// <inheritdoc/>
        public abstract HostingEnvironment HostingEnvironment { get; }

        /// <inheritdoc/>
        public abstract void Validate(ClusterDefinition clusterDefinition);

        /// <inheritdoc/>
        public virtual bool RequiresNodeAddressCheck => false;

        /// <inheritdoc/>
        public abstract void AddProvisioningSteps(SetupController<NodeDefinition> controller);

        /// <inheritdoc/>
        public virtual void AddPostProvisioningSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public virtual void AddSetupSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public virtual void AddPostSetupSteps(SetupController<NodeDefinition> controllerd)
        {
        }

        /// <inheritdoc/>
        public virtual bool CanManageRouter => false;

        /// <inheritdoc/>
        public virtual async Task UpdateInternetRoutingAsync()
        {
            await SyncContext.Clear;
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task EnableInternetSshAsync()
        {
            await SyncContext.Clear;
            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public virtual async Task DisableInternetSshAsync()
        {
            await SyncContext.Clear;
        }

        /// <inheritdoc/>
        public abstract (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <inheritdoc/>
        public abstract string GetDataDisk(LinuxSshProxy node);

        /// <inheritdoc/>
        public abstract IEnumerable<string> GetClusterAddresses();

        /// <summary>
        /// Used by cloud and potentially other hosting manager implementations to verify the
        /// node address assignments and/or to automatically assign these addresses.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <remarks>
        /// <note>
        /// This method verifies that node addresses for on-premise environments are located
        /// within the premise subnet.  The method will not attempt to assign node addresses 
        /// for on-premise node and requires all nodes have explicit addresses.
        /// </note>
        /// </remarks>
        protected void AssignNodeAddresses(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var networkOptions = clusterDefinition.Network;

            // Verify that explicit address assignments are not duplicated
            // across any nodes.

            var addressToNode = new Dictionary<IPAddress, NodeDefinition>();

            foreach (var node in clusterDefinition.SortedNodes)
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                var address = NetHelper.ParseIPv4Address(node.Address);

                if (addressToNode.TryGetValue(address, out var conflictNode))
                {
                    throw new ClusterDefinitionException($"Nodes [{conflictNode.Name}] and [{node.Name}] both specify the same address [{address}].  Node addresses must be unique.");
                }

                addressToNode.Add(address, node);
            }

            if (KubeHelper.IsOnPremiseEnvironment(clusterDefinition.Hosting.Environment))
            {
                // Verify that all nodes have explicit addresses for on-premise environments.

                foreach (var node in clusterDefinition.SortedNodes)
                {
                    if (string.IsNullOrEmpty(node.Address))
                    {
                        throw new ClusterDefinitionException($"Node [{node.Name}] is not assigned an address.  All nodes must have explicit IP addresses for on-premise hosting environments.");
                    }
                }

                return;
            }

            // Ensure that any explicit node IP address assignments are located
            // within the subnet where the nodes will be provisioned and do not 
            // conflict with any of the addresses reserved by the cloud provider
            // or neonKUBE.

            var nodeSubnetInfo = clusterDefinition.NodeSubnet;
            var nodeSubnet     = NetworkCidr.Parse(nodeSubnetInfo.Subnet);

            if (clusterDefinition.Nodes.Count() > nodeSubnet.AddressCount - nodeSubnetInfo.ReservedAddresses)
            {
                throw new ClusterDefinitionException($"The cluster includes [{clusterDefinition.Nodes.Count()}] nodes which will not fit within the [{nodeSubnet}] target subnet after accounting for [{nodeSubnetInfo.ReservedAddresses}] reserved addresses.");
            }

            var firstValidAddressUint = NetHelper.AddressToUint(nodeSubnet.FirstAddress) + KubeConst.CloudSubnetStartReservedIPs;
            var firstValidAddress = NetHelper.UintToAddress(firstValidAddressUint);
            var lastValidAddressUint = NetHelper.AddressToUint(nodeSubnet.LastAddress) - KubeConst.CloudSubnetEndReservedIPs;
            var lastValidAddress = NetHelper.UintToAddress(lastValidAddressUint);

            foreach (var node in clusterDefinition.SortedNodes.OrderBy(node => node.Name))
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    // Ignore nodes with unassigned addresses.

                    continue;
                }

                var address = NetHelper.ParseIPv4Address(node.Address);

                if (!nodeSubnet.Contains(address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] is assigned [{node.Address}={node.Address}] which is outside of the [{nodeSubnet}].");
                }

                var addressUint = NetHelper.AddressToUint(address);

                if (addressUint < firstValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The first valid node address for subnet [{nodeSubnet}] is [{firstValidAddress}].");
                }

                if (addressUint > lastValidAddressUint)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] defines IP address [{node.Address}={node.Address}] which is reserved.  The last valid node address for subnet [{nodeSubnet}] is [{lastValidAddress}].");
                }
            }

            //-----------------------------------------------------------------
            // Automatically assign unused IP addresses within the subnet to nodes that 
            // were not explicitly assigned an address in the cluster definition.

            var assignedAddresses = new HashSet<uint>();

            foreach (var node in clusterDefinition.SortedNodes)
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    continue;
                }

                var address = NetHelper.ParseIPv4Address(node.Address);
                var addressUint = NetHelper.AddressToUint(address);

                if (!assignedAddresses.Contains(addressUint))
                {
                    assignedAddresses.Add(addressUint);
                }
            }

            foreach (var azureNode in clusterDefinition.SortedControlThenWorkerNodes)
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

        //---------------------------------------------------------------------
        // Cluster life cycle methods

        /// <summary>
        /// The default timeout for <see cref="GetClusterHealthAsync(TimeSpan)"/> implementations.
        /// </summary>
        protected readonly TimeSpan DefaultStatusTimeout = TimeSpan.FromSeconds(15);

        /// <inheritdoc/>
        public abstract HostingCapabilities Capabilities { get; }

        /// <inheritdoc/>
        public abstract Task<HostingResourceAvailability> GetResourceAvailabilityAsync(long reserveMemory = 0, long reserveDisk = 0);

        /// <inheritdoc/>
        public abstract Task<ClusterHealth> GetClusterHealthAsync(TimeSpan timeout = default);

        /// <inheritdoc/>
        public virtual async Task StartClusterAsync()
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public virtual async Task StopClusterAsync(StopMode stopMode = StopMode.Graceful)
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public virtual async Task PauseClusterAsync()
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public virtual async Task ResumeClusterAsync()
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public virtual async Task DeleteClusterAsync(bool removeOrphans = false)
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }
    }
}
