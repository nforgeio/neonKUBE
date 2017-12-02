//-----------------------------------------------------------------------------
// FILE:	    HostedEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a network endpoint to be exposed by a neonCLUSTER deployed to
    /// hosting providers such as AWS, Azure, or Google by platform load platform
    /// load balancers.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Docker ingress networks don't currently support forwarding of UDP traffic
    /// to services.  HAProxy doesn't handle UDP either so UDP endpoints aren't
    /// super useful at this point.
    /// </note>
    /// <para>
    /// A hosted endpoint controls which external network traffic is routed into
    /// a neonCLUSTER by specifying the external network port where the traffic is
    /// received and the internal cluster port where the traffic will be routed.
    /// This also specifies whether the traffic is to be treated as TCP or UDP.
    /// </para>
    /// <para>
    /// This is typically used to route external TCP or UDP traffic to the
    /// cluster's <b>neon-proxy-public</b> via the Docker ingress network during
    /// cluster setup, by configuring a load balancer to balance traffic across
    /// all Docker nodes.  The ingress network will take care of forwarding traffic
    /// to the <b>neon-proxy-public</b> instances which will handle SSL termination
    /// (if required) and then forward traffic onto the target Docker service.
    /// </para>
    /// </remarks>
    public class HostedEndpoint
    {
        private const int defaultIdleTimeout = 5;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HostedEndpoint()
        {
        }

        /// <summary>
        /// Parametized constructor.
        /// </summary>
        /// <param name="protocol">Specifies the protocol.</param>
        /// <param name="externalPort">Specifies the external network port.</param>
        /// <param name="internalPort">Specifies the internal cluster network port.</param>
        public HostedEndpoint(HostedEndpointProtocol protocol, int externalPort, int internalPort)
        {
            this.Protocol     = protocol;
            this.FrontendPort = externalPort;
            this.BackendPort  = internalPort;
        }

        /// <summary>
        /// Specifies the network protocol to be supported.  This defaults to <see cref="HostedEndpointProtocol.Tcp"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostedEndpointProtocol.Tcp)]
        public HostedEndpointProtocol Protocol { get; set; } = HostedEndpointProtocol.Tcp;

        /// <summary>
        /// Specifies the external network port from which traffic is to be routed into the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "FrontendPort", Required = Required.Always)]
        public int FrontendPort { get; set; }

        /// <summary>
        /// Specifies the internal cluster port where the traffic is to be routed.
        /// </summary>
        [JsonProperty(PropertyName = "BackendPort", Required = Required.Always)]
        public int BackendPort { get; set; }

        /// <summary>
        /// Maximum time an external connection to this endpoint will remain open
        /// while idle.  This defaults to <b>5</b> minutes.
        /// </summary>
        /// <remarks>
        /// <note>
        /// <para>
        /// Cloud providers support various ranges:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>AWS</b></term>
        ///     <description>
        ///     $todo(jeff.lill): Figure this out.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Azure</b></term>
        ///     <description>
        ///     Between 4 and 30 minutes.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Google</b></term>
        ///     <description>
        ///     $todo(jeff.lill): Figure this out.
        ///     </description>
        /// </item>
        /// </list>
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "IdleTimeoutMinutes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultIdleTimeout)]
        public int IdleTimeoutMinutes { get; set; } = defaultIdleTimeout;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!NetHelper.IsValidPort(FrontendPort))
            {
                throw new ClusterDefinitionException($"[{nameof(HostedEndpoint)}.{nameof(FrontendPort)}] value [{FrontendPort}] is outside the range of a valid network port.");
            }

            if (!NetHelper.IsValidPort(FrontendPort))
            {
                throw new ClusterDefinitionException($"[{nameof(HostedEndpoint)}.{nameof(BackendPort)}] value [{BackendPort}] is outside the range of a valid network port.");
            }
        }
    }
}
