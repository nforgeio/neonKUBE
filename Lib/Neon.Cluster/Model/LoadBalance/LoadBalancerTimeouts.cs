//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerTimeouts.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes load balancer timeouts.
    /// </summary>
    public class LoadBalancerTimeouts
    {
        /// <summary>
        /// The maximum time to wait for a connection attempt to a server.
        /// </summary>
        [JsonProperty(PropertyName = "ConnectSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double ConnectSeconds { get; set; } = 5.0;

        /// <summary>
        /// The maximum time to wait for a client to continue transmitting a
        /// request.  This defaults to <b>50 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double ClientSeconds { get; set; } = 50.0;

        /// <summary>
        /// The maximum time to wait for a server to acknowledge or transmit
        /// data.  This defaults to <b>50 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ServerSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double ServerSeconds { get; set; } = 50.0;

        /// <summary>
        /// The maximum time to wait for a health check.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public double CheckSeconds { get; set; } = 5.0;

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(LoadBalancerValidationContext context)
        {
            if (ConnectSeconds <= 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ConnectSeconds)}={ConnectSeconds}] is not positive.");
            }

            if (ClientSeconds <= 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ClientSeconds)}={ClientSeconds}] is not positive.");
            }

            if (ServerSeconds <= 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ServerSeconds)}={ServerSeconds}] is not positive.");
            }

            if (CheckSeconds <= 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(CheckSeconds)}={CheckSeconds}] is not positive.");
            }
        }
    }
}
