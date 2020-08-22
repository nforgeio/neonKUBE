//-----------------------------------------------------------------------------
// FILE:	    HostedEndpoint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Kube
{
    /// <summary>
    /// Describes a network endpoint to be exposed by a neonKUBE cluster deployed to
    /// hosting providers such as AWS, Azure, or Google by platform load platform
    /// load balancers.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// The <a href="https://istio.io">Istio</a> service mesh component used by
    /// neonKUBE clusters to route network traffic in/out of the cluster as well
    /// as between internal services and pods doesn't currently support UDP traffic,
    /// do UDP endpoints are not useful now.  UDP support is on their radar though
    /// and the underlying Envoy project has been making progress with UDP as well,
    /// so perhaps we'll be able to enable this scenario in the future.  Here's
    /// the tracking issue:
    /// </para>
    /// <para>
    /// <a href="https://github.com/istio/istio/issues/1430">https://github.com/istio/istio/issues/1430</a>
    /// </para>
    /// </note>
    /// <para>
    /// Most clusters will generally sit behind a load balancer or router that forwards
    /// external network traffic into the cluster based on the endpoints found in 
    /// the cluster definition.  The endpoints specify the network protocol (TCP/UDP),
    /// the external load balancer port the traffic is received on and the cluster node 
    /// port where the traffic will be forwarded.
    /// </para>
    /// <para>
    /// The load balancer will balance traffic against one or more cluster nodes.  These
    /// nodes will have their <c>node.ingress=true</c> label set.  Cluster setup will 
    /// use this to deploy Istio on these nodes and will also configure the load balancer
    /// (when posible) to load balance external traffic across these nodes.  Istio will
    /// then handle routing traffic to the cluster services and pods.
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
        /// Validates the options and also ensures that all <c>null</c> properties are
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
