//-----------------------------------------------------------------------------
// FILE:	    ProxyTcpFrontend.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a TCP proxy frontend.
    /// </summary>
    public class ProxyTcpFrontend
    {
        /// <summary>
        /// The TCP HAProxy frontend port for this route.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyPort", Required = Required.Always)]
        public int ProxyPort { get; set; }

        /// <summary>
        /// <para>
        /// The network port to be exposed for this route on the clusters public Internet facing load balancer.
        /// This defaults to <b>0</b> for TCP routes.  Only routes with positive public ports will be exposed
        /// to to the public Internet via the load balancer.
        /// </para>
        /// <note>
        /// This is honored only for <b>public</b> proxy routes.  Public ports for <b>private</b> proxies will be ignored.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PublicPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int PublicPort { get; set; } = 0;

        /// <summary>
        /// Validates the frontend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="route">The parent route.</param>
        public void Validate(ProxyValidationContext context, ProxyTcpRoute route)
        {
            if (ProxyPort < context.Settings.FirstTcpPort || context.Settings.LastPort < ProxyPort)
            {
                context.Error($"Route [{route.Name}] assigns [{nameof(ProxyPort)}={ProxyPort}] which is outside the range of valid frontend TCP ports for this proxy [{context.Settings.FirstTcpPort}...{context.Settings.LastPort}].");
            }

            if (PublicPort > 0 && !NetHelper.IsValidPort(PublicPort))
            {
                context.Error($"Proxy [{nameof(PublicPort)}={PublicPort}] is not a valid network port.");
            }
        }
    }
}
