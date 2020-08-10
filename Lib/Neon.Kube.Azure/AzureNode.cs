//-----------------------------------------------------------------------------
// FILE:	    AzureNode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using Microsoft.Azure.Management.Compute.Fluent;
using Microsoft.Azure.Management.Compute.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Network.Fluent;
using Microsoft.Azure.Management.Network.Fluent.LoadBalancer.Definition;
using Microsoft.Azure.Management.Network.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;

using Neon.Net;

using AzureEnvironment = Microsoft.Azure.Management.ResourceManager.Fluent.AzureEnvironment;

namespace Neon.Kube
{
    /// <summary>
    /// Holds information about a cluster node VM within the context of an Azure deployment. 
    /// </summary>
    internal class AzureNode
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">The associated node proxy.</param>
        public AzureNode(SshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            this.Node = node;
        }

        /// <summary>
        /// Returns the associated SSH node proxy.
        /// </summary>
        public SshProxy<NodeDefinition> Node { get; private set; }

        /// <summary>
        /// Returns the node name.
        /// </summary>
        public string Name => Node.Name;

        /// <summary>
        /// The node's associated Azure VM.
        /// </summary>
        public IVirtualMachine Vm { get; set; }

        /// <summary>
        /// The node's network interface within the Azure <b>VNET</b>.
        /// </summary>
        public INetworkInterface Nic { get; set; }

        /// <summary>
        /// Returns the node's private IP address within the Azure <b>VNET</b>.
        /// </summary>
        public IPAddress PrivateAddress => Node.PrivateAddress;

        /// <summary>
        /// Nodes may be accessed externally via SSH when a NAT rule is configured
        /// for the cluster load balancer.  This can be set to the public port on
        /// the load balancer that is 
        /// </summary>
        public int SshNatPort { get; set; } = 0;

        /// <summary>
        /// Returns the Azure name for the temporary NAT rule mapping a 
        /// cluster load balancer port to the SSH port for this node.
        /// </summary>
        public string SshNatRuleName => $"neon-ssh-tcp-{Node.Name}";

        /// <summary>
        /// Returns the node's role.
        /// </summary>
        public string Role => Node.Metadata.Role;
    }
}
