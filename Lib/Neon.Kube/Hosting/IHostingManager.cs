//-----------------------------------------------------------------------------
// FILE:	    IHostingManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Interface describing the hosting environment managers.
    /// </summary>
    public interface IHostingManager : IDisposable
    {
        /// <summary>
        /// Returns <c>true</c> if the provisioning operation actually does nothing.
        /// </summary>
        bool IsProvisionNOP { get; }

        /// <summary>
        /// Verifies that a cluster is valid for the hosting manager, customizing 
        /// properties as required.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if any problems were detected.</exception>
        void Validate(ClusterDefinition clusterDefinition);

        /// <summary>
        /// Returns <c>true</c> if provisoning requires that the user have
        /// administrator privileges.
        /// </summary>
        bool RequiresAdminPrivileges { get; }

        /// <summary>
        /// Specifies whether a cryptographically random node password should be generated.
        /// </summary>
        bool GenerateSecurePassword { get; }

        /// <summary>
        /// Creates and initializes the cluster resources such as the virtual machines,
        /// networks, load balancers, network security groups, public IP addresses etc.
        /// </summary>
        /// <param name="clusterLogin">
        /// The login for the cluster being provisioned.
        /// This is required.
        /// </param>
        /// <param name="secureSshPassword">
        /// The secure SSH password to be set for all node VMs. This is required.
        /// </param>
        /// <param name="orgSshPassword">
        /// The starting SSH password for the VMs.  This may be passed as <c>null</c> when
        /// the provisioning manager is able to configure the passwords when the VMs are
        /// born, such as in the cloud or when hosted via on-premise hypervisors.  This
        /// is currently used only by the bare metal hosting manager which will need to
        /// be able to log into existing nodes provisioned manually by the cluster operator.
        /// </param>
        /// <returns><c>true</c> on success.</returns>
        /// <remarks>
        /// <para>
        /// For the clusters that return <see cref="CanManageRouter"/>=<c>true</c> (typically
        /// cloud managers  indicating that they can manage the upstream router or load balancer) 
        /// this method will leave the public SSH NAT rules in place so that cluster provisioning
        /// and setup will be able to establish SSH connections to each cluster node.  This is
        /// equivalent to calling <see cref="EnableInternetSshAsync"/>.
        /// </para>
        /// </remarks>
        Task<bool> ProvisionAsync(ClusterLogin clusterLogin, string secureSshPassword, string orgSshPassword = null);

        /// <summary>
        /// Adds any steps to be performed after the node has been otherwise prepared.
        /// </summary>
        /// <param name="setupController">The target setup controller.</param>
        void AddPostPrepareSteps(SetupController<NodeDefinition> setupController);

        /// <summary>
        /// Returns <c>true</c> if the hosting manage is capable of updating the upstream
        /// network router or load balancer.  Cloud based managers will return <c>true</c>
        /// whereas on-premise managers will return <c>false</c> because we don't have
        /// the ability to manage physical routers yet.
        /// </summary>
        bool CanManageRouter { get; }

        /// <summary>
        /// <para>
        /// Updates the cluster's load balancer or router to use the current set of
        /// ingress rules defined by <see cref="NetworkOptions.IngressRules"/> and the
        /// egress rules defined by <see cref="NetworkOptions.EgressAddressRules"/>.
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like for Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task UpdateInternetRoutingAsync();

        /// <summary>
        /// <para>
        /// Enables public SSH access for every node in the cluster, honoring source
        /// address limitations specified by <see cref="NetworkOptions.ManagementAddressRules"/>
        /// in the cluster definition.
        /// </para>
        /// <para>
        /// Each node will be assigned a public port that has a NAT rule directing SSH
        /// traffic to that specific node.  These ports will be in the range of
        /// <see cref="NetworkOptions.FirstExternalSshPort"/> to <see cref="NetworkOptions.LastExternalSshPort"/>.
        /// <see cref="GetSshEndpoint(string)"/> will return the external endpoint
        /// for nodes when external SSH is enabled. 
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like: Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task EnableInternetSshAsync();

        /// <summary>
        /// <para>
        /// Disables public SSH access for every node in the cluster, honoring source
        /// address limitations specified by <see cref="NetworkOptions.ManagementAddressRules"/>
        /// in the cluster definition.
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like: Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task DisableInternetSshAsync();

        /// <summary>
        /// Returns the FQDN or IP address (as a string) and the port to use
        /// to establish a SSH connection to a specific node. 
        /// </summary>
        /// <param name="nodeName">The target node's name.</param>
        /// <returns>A <b>(string Address, int Port)</b> tuple.</returns>
        /// <remarks>
        /// This will return the direct private node endpoint by default.  If
        /// <see cref="EnableInternetSshAsync"/> has been called and is supported by 
        /// the hosting manager, then this returns the public address of the
        /// cluster along with the public NAT port.
        /// </remarks>
        (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <summary>
        /// Identifies the data disk device for a node.  This returns the data disk's device 
        /// name when an unitialized data disk exists or "PRIMARY" when the  OS disk
        /// will be used for data.
        /// </summary>
        /// <returns>The disk device name or "PRIMARY".</returns>
        /// <remarks>
        /// <note>
        /// This will not work after the node's data disk has been initialized.
        /// </note>
        /// </remarks>
        string GetDataDisk(SshProxy<NodeDefinition> node);

        /// <summary>
        /// Returns the name for a disk partition.
        /// </summary>
        /// <param name="diskName">The disk name (like: <b>"/dev/sda"</b>).</param>
        /// <param name="partition">The partition number (between 1..4).</param>
        /// <returns>The disk partition name.</returns>
        /// <remarks>
        /// <para>
        /// For most hosting environments, the partition name is simply the
        /// partition number appended to the disk name like <b>"/dev/sda1"</b>,
        /// but this is not always the case.  For example, AWS names disks
        /// like <b>"/dev/nvme0n1"</b> and partitions like <b>/dev/nvme0n1p1</b>
        /// </para>
        /// <para>
        /// This method gives hosting managers a chance to return a custom
        /// name when necessary.  The base <see cref="HostingManager.GetPartitionName(string, int)"/>
        /// method simply appends the partition number to the disk name.
        /// </para>
        /// </remarks>
        string GetPartitionName(string diskName, int partition);
    }
}
