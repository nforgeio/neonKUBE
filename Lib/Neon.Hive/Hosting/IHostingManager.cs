//-----------------------------------------------------------------------------
// FILE:	    IHostingManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
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
        /// Verifies that a hive is valid for the hosting manager, customizing 
        /// properties as required.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if any problems were detected.</exception>
        void Validate(HiveDefinition hiveDefinition);

        /// <summary>
        /// Creates and initializes the hive resources such as the virtual machines,
        /// networks, load balancers, network security groups, public IP addresses etc.
        /// </summary>
        /// <param name="force">
        /// Indicates that any existing resources (such as virtual machines) 
        /// are to be replaced or overwritten during privisioning.  The actual interpretation
        /// of this parameter is specific to each hosting manager implementation.
        /// </param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        bool Provision(bool force);

        /// <summary>
        /// Returns the FQDN or IP address (as a string) and the port to use
        /// to establish a SSH connection to a node while provisioning is in
        /// progress.
        /// </summary>
        /// <param name="nodeName">The target node's name.</param>
        /// <returns>A <b>(string Address, int Port)</b> tuple.</returns>
        /// <remarks>
        /// Hosting platforms such as Azure that may not assign public IP addresses
        /// to hive nodes will return the IP address of the traffic manager and
        /// a temporary NAT port for the node.
        /// </remarks>
        (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <summary>
        /// Adds any necessary post-provisioning steps to the step controller.
        /// </summary>
        /// <param name="controller">The target setup controller.</param>
        void AddPostProvisionSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Adds any necessary post-VPN steps to the step controller.
        /// </summary>
        /// <param name="controller">The target setup controller.</param>
        void AddPostVpnSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Returns the endpoints currently exposed to the public for the deployment.
        /// </summary>
        /// <returns>The list of <see cref="HostedEndpoint"/> instances.</returns>
        List<HostedEndpoint> GetPublicEndpoints();

        /// <summary>
        /// Returns <c>true</c> if the hive manager is able to update the deployment's load balancer and security rules.
        /// </summary>
        bool CanUpdatePublicEndpoints { get; }

        /// <summary>
        /// Updates the deployment's load balancer and security rules to allow traffic 
        /// for the specified endpoints into the hive.
        /// </summary>
        /// <param name="endpoints">The endpoints.</param>
        void UpdatePublicEndpoints(List<HostedEndpoint> endpoints);

        /// <summary>
        /// <para>
        /// Returns the prefix for block devices that will be attached to
        /// the host machines.  For many hosting environments this will be
        /// <b>sd</b>, indicating that drives will be attached like: 
        /// <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// <para>
        /// This may be different though for some hosting environment.
        /// XenServer for example, uses the <b>xvd</b> prefix and attaches
        /// drives as <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// </summary>
        string DrivePrefix { get; }

        /// <summary>
        /// Returns <c>true</c> if provisoning requires that the user has
        /// administrator privileges.
        /// </summary>
        bool RequiresAdminPrivileges { get; }
    }
}
