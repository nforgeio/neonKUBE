//-----------------------------------------------------------------------------
// FILE:        HostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.SSH;
using Neon.Tasks;
using Neon.Time;

namespace Neon.Kube.Hosting
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

        /// <summary>
        /// <b>HACK:</b> Used by derived <see cref="HostingManager"/> implementations to defeat
        /// C# code optimization to prevent trimming.
        /// </summary>
        /// <param name="action">Specifies an action that ensures that trimming doesn't happen.</param>
        protected static void Load(Action action)
        {
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));

            // $hack(jefflill):
            //
            // We fetch an environment variable what we don't expect to exist and create these
            // instances.  The variable check will ensure that the C# compiler won't optimize
            // these calls out but if on the off chance, the variable exists we'll invoke the
            // action.

            if (Environment.GetEnvironmentVariable("notfound-08749E0B-3EE0-48B6-93DF-245285147ED1") != null)
            {
                action.Invoke();
            }
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
        public abstract Task CheckDeploymentReadinessAsync(ClusterDefinition clusterDefinition);

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
        public virtual async Task<(double? Latitude, double? Longitude)> GetDatacenterCoordinatesAsync()
        {
            await SyncContext.Clear;

            return (Latitude: null, Longitude: null);
        }

        /// <inheritdoc/>
        public virtual async Task<string> CheckForConflictsAsync(ClusterDefinition clusterDefinition) => await Task.FromResult((string)null);

        /// <summary>
        /// Used by on-premise hosting managers to detect IP address related conflicts.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <returns>
        /// <c>null</c> when there are no conflicts, otherise a string detailing
        /// the conflicts.
        /// </returns>
        protected async Task<string> CheckForIPConflictsAsync(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            // $todo(jefflill):
            //
            // The ARP cache lookups need more testing and will be disabled until then.
            // We've seen situations when deploying a NeonDESKTOP cluster when the
            // cluster IP is reported as being in use.  This may also impact other
            // clusters as well.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1838

            // We're going to send ICMP pings to all node IP addresses and keep track
            // of any responses (indicating conflcits) and then read the local machine's
            // ARP table looking for any conflicts there.
            //
            // NOTE: It's possible for machines to have ICMP ping disabled so we
            //       won't see a response from them, but there's a decent chance that
            //       those machines will have locally cached ARP records.

            var nodeConflicts = new Dictionary<string, NodeDefinition>(StringComparer.InvariantCultureIgnoreCase);

#if TODO
            using (var pinger = new Pinger())
            {
                await Parallel.ForEachAsync(clusterDefinition.NodeDefinitions.Values, new ParallelOptions() { MaxDegreeOfParallelism = 50 },
                    async (nodeDefinition, cancellationToken) =>
                    {
                        var reply = await pinger.SendPingAsync(nodeDefinition.Address);

                        if (reply.Status == IPStatus.Success)
                        {
                            // We got a response.

                            lock (nodeConflicts)
                            {
                                nodeConflicts.Add(nodeDefinition.Name, nodeDefinition);
                            }
                        }
                        else
                        {
                            // We didn't get a response.  There are three possibilities
                            // to consider:
                            //
                            //      1. There's a machine on this IP but it's configured to ignore pings
                            //      2. There no machine on this IP but ARP table may still be caching an entry
                            //      3. There's no machine on this IP and there's no cached ARP entry
                            //
                            // We're going to clear the ARP entry for this IP and then re-ping it.  This
                            // will refresh the ARP entry if the machine exists which is important for
                            // the check below.

                            NetHelper.DeleteArpEntry(IPAddress.Parse(nodeDefinition.Address));

                            await pinger.SendPingAsync(nodeDefinition.Address);

                            if (reply.Status == IPStatus.Success)
                            {
                                // We got a response this time.

                                lock (nodeConflicts)
                                {
                                    nodeConflicts.Add(nodeDefinition.Name, nodeDefinition);
                                }
                            }
                        }
                    });
            }

            // Get the ARP table for the workstation and look for any IP addresses
            // that conflict with cluster nodes that are not already captured as
            // conflicted.

            var arpTable = await NetHelper.GetArpFlatTableAsync();

            foreach (var nodeDefinition in clusterDefinition.NodeDefinitions.Values)
            {
                if (arpTable.ContainsKey(IPAddress.Parse(nodeDefinition.Address)))
                {
                    nodeConflicts[nodeDefinition.Name] = nodeDefinition;
                }
            }
#endif
            using (var pinger = new Pinger())
            {
                await Parallel.ForEachAsync(clusterDefinition.NodeDefinitions.Values, new ParallelOptions() { MaxDegreeOfParallelism = 50 },
                    async (nodeDefinition, cancellationToken) =>
                    {
                        var reply = await pinger.SendPingAsync(nodeDefinition.Address);

                        if (reply.Status == IPStatus.Success)
                        {
                            // We got a response.

                            lock (nodeConflicts)
                            {
                                nodeConflicts.Add(nodeDefinition.Name, nodeDefinition);
                            }
                        }
                    });
            }

            if (nodeConflicts.Count == 0)
            {
                return null;
            }

            var sb        = new StringBuilder();
            var separator = new string('-', 40);

            sb.AppendLine($"[{nodeConflicts.Count}] cluster nodes have IP conflicts with other computers:");
            sb.AppendLine(separator);

            foreach (var nodeDefinition in nodeConflicts.Values.
                OrderBy(nodeDefinition => nodeDefinition.Name, StringComparer.InvariantCultureIgnoreCase))
            {
                sb.AppendLine($"{nodeDefinition.Name}/{nodeDefinition.Address}");
            }

            sb.AppendLine(separator);

            return sb.ToString();
        }

        /// <inheritdoc/>
        public abstract IEnumerable<string> GetClusterAddresses();

        /// <summary>
        /// Used by cloud and potentially other hosting manager implementations to verify the
        /// node address assignments and/or to automatically assign these addresses.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
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
            // or NeonKUBE.

            var nodeSubnetInfo = clusterDefinition.NodeSubnet;
            var nodeSubnet     = NetworkCidr.Parse(nodeSubnetInfo.Subnet);

            if (clusterDefinition.Nodes.Count() > nodeSubnet.AddressCount - nodeSubnetInfo.ReservedAddresses)
            {
                throw new ClusterDefinitionException($"The cluster includes [{clusterDefinition.Nodes.Count()}] nodes which will not fit within the [{nodeSubnet}] target subnet after accounting for [{nodeSubnetInfo.ReservedAddresses}] reserved addresses.");
            }

            var firstValidAddressUint = NetHelper.AddressToUint(nodeSubnet.FirstAddress) + KubeConst.CloudSubnetStartReservedIPs;
            var firstValidAddress     = NetHelper.UintToAddress(firstValidAddressUint);
            var lastValidAddressUint  = NetHelper.AddressToUint(nodeSubnet.LastAddress) - KubeConst.CloudSubnetEndReservedIPs;
            var lastValidAddress      = NetHelper.UintToAddress(lastValidAddressUint);

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

        /// <summary>
        /// Performs final cluster definition validation including ensuring that the vCPUs and
        /// memory assigned to each node is supported, adding any details to the <see cref="HostingReadiness"/>
        /// instance passed.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <param name="hostedNodes">
        /// Specifies information about each cluster node including the number of vCPUs
        /// and memory (derived from the instance type/size for cloud environments).
        /// </param>
        /// <param name="readiness">Used to return discovered readiness problems.</param>
        /// <remarks>
        /// <para>
        /// NeonKUBE clusters supports control-plane nodes with 2+ cores and at least 8GB RAM.
        /// All worker nodes must have at least 4 cores and at least 8GiB RAM.  Clusters that
        /// have control-plane nodes with only 2 cores are required to have at least 1 worker
        /// node.
        /// </para>
        /// <note>
        /// For cloud environments, we don't charge an extra hourly fee for 2-core VMs hosting
        /// control-plane nodes to be more competitive with cloud integrated Kubernetes offerings
        /// like AKS/EKS where the control-plane is entirely free.  We need to ensure users can't
        /// workaround our fees by deploying 2-core worker nodes.
        /// </note>
        /// </remarks>
        protected void ValidateCluster(ClusterDefinition clusterDefinition, List<HostedNodeInfo> hostedNodes, HostingReadiness readiness)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(hostedNodes != null, nameof(hostedNodes));
            Covenant.Requires<ArgumentException>(hostedNodes.Count > 0, nameof(hostedNodes));
            Covenant.Requires<ArgumentNullException>(readiness != null, nameof(readiness));

            var minMemory = 4 * ByteUnits.GigaBytes;

            // Verify that control-plane nodes have at least 2 vCPUs and 8GB RAM.

            foreach (var node in hostedNodes.Where(node => node.Role == NodeRole.ControlPlane))
            {
                if (node.VCpus < KubeConst.MinControlNodeVCpus && node.Memory < minMemory)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Control-plane node [{node.Name}] has only [{node.VCpus}] vCPUs and [{ByteUnits.Humanize(node.Memory, powerOfTwo: false)}] memory.  At least [{KubeConst.MinControlNodeVCpus}] vCPUs and [{ByteUnits.Humanize(minMemory, powerOfTwo: false)}] memory is required.");
                }
                else if (node.VCpus < KubeConst.MinControlNodeVCpus)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Control-plane node [{node.Name}] has only [{node.VCpus}] vCPUs.  At least [{KubeConst.MinControlNodeVCpus}] vCPUs are required.");
                }
                else if (node.Memory < minMemory)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Control-plane node [{node.Name}] has only [{ByteUnits.Humanize(node.Memory, powerOfTwo: false)}] memory.  At least [{ByteUnits.Humanize(minMemory, powerOfTwo: false)}] memory is required.");
                }
            }

            // Verify that worker nodes have at least 4 vCPUs and 8GB RAM.

            foreach (var node in hostedNodes.Where(node => node.Role == NodeRole.Worker))
            {
                if (node.VCpus < KubeConst.MinWorkerNodeVCpus && node.Memory < minMemory)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Worker node [{node.Name}] has too only [{node.VCpus}] vCPUs and [{ByteUnits.Humanize(node.Memory, powerOfTwo: false)}] memory.  At least [{KubeConst.MinWorkerNodeVCpus}] vCPUs and [{ByteUnits.Humanize(minMemory, powerOfTwo: false)}] memory is required.");
                }
                else if (node.VCpus < KubeConst.MinWorkerNodeVCpus)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Worker node [{node.Name}] has too only [{node.VCpus}] vCPUs.  At least [{KubeConst.MinWorkerNodeVCpus}] vCPUs are required.");
                }
                else if (node.Memory < minMemory)
                {
                    readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: $"Worker node [{node.Name}] has [{ByteUnits.Humanize(node.Memory, powerOfTwo: false)}] memory.  At least [{ByteUnits.Humanize(minMemory, powerOfTwo: false)}] memory is required.");
                }
            }

            // Clusters that have control-plane nodes with just 2 cores require at
            // least 1 worker node.

            if (hostedNodes.Any(node => node.Role == NodeRole.ControlPlane && node.VCpus <= 2) && hostedNodes.Count(node => node.Role == NodeRole.Worker) == 0)
            {
                readiness.AddProblem(type: HostingReadinessProblem.ClusterDefinitionType, details: "Clusters must have at least one worker node when any of the control-plane nodes has only 2 vCPUs.");
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
        public virtual async Task DeleteClusterAsync(ClusterDefinition clusterDefinition = null)
        {
            await SyncContext.Clear;
            throw new NotSupportedException();
        }
    }
}
