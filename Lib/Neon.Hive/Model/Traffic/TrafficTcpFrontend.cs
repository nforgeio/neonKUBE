//-----------------------------------------------------------------------------
// FILE:	    TrafficTcpFrontend.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Hive
{
    /// <summary>
    /// Describes a TCP traffic manager frontend.
    /// </summary>
    public class TrafficTcpFrontend : TrafficFrontend
    {
        /// <summary>
        /// The TCP HAProxy frontend port for this rule.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyPort", Required = Required.Always)]
        public int ProxyPort { get; set; }

        /// <summary>
        /// <para>
        /// The network port to be exposed for this rule on the hive's public Internet facing load balancer.
        /// This defaults to <b>0</b> for TCP rules.  Only rules with positive public ports will be exposed
        /// to the public Internet via the load balancer.
        /// </para>
        /// <note>
        /// This is honored only for <b>public</b> traffic manager rules.  Public ports for <b>private</b> proxies will be ignored.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PublicPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int PublicPort { get; set; } = 0;

        /// <summary>
        /// Validates the frontend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficTcpRule rule)
        {
            base.Validate(context, rule);

            if (PublicPort > 0 && !NetHelper.IsValidPort(PublicPort))
            {
                context.Error($"Load balancer [{nameof(PublicPort)}={PublicPort}] is not a valid network port.");
            }
            
            if (!context.Settings.ProxyPorts.IsValidTcpPort(ProxyPort))
            {
                context.Error($"Rule [{rule.Name}] assigns [{nameof(ProxyPort)}={ProxyPort}] which is outside the range of valid frontend TCP ports for this traffic manager [{context.Settings.ProxyPorts}].");
            }
        }
    }
}
