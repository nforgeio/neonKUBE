//-----------------------------------------------------------------------------
// FILE:	    TrafficTimeouts.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Describes traffic manager timeouts.
    /// </summary>
    public class TrafficTimeouts
    {
        /// <summary>
        /// The default maximum time to wait for a connection attempt to a server (<b>5 seconds</b>).
        /// </summary>
        public const double DefaultConnectSeconds = 5.0;

        /// <summary>
        /// The default maximum time to wait for a client to continue transmitting a
        /// request (<b>50 seconds</b>).
        /// </summary>
        public const double DefaultClientSeconds = 50.0;

        /// <summary>
        /// The default maximum time to wait for a server to acknowledge or transmit
        /// data (<b>50 seconds</b>).
        /// </summary>
        public const double DefaultServerSeconds = 50.0;

        /// <summary>
        /// The default maximum time to wait for a health check (<b>5 seconds</b>).
        /// </summary>
        public const double DefaultCheckSeconsds = 5.0;

        /// <summary>
        /// The default maximum time to keep a client side HTTP connection open after
        /// returning the first response to wait for a another client request.
        /// </summary>
        public const double DefaultClientHttpKeepAliveSeconds = 0.5;

        /// <summary>
        /// The maximum time to wait for a connection attempt to a server.
        /// This defaults to <b>5 seconds</b>. 
        /// </summary>
        [JsonProperty(PropertyName = "ConnectSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultConnectSeconds)]
        public double ConnectSeconds { get; set; } = DefaultConnectSeconds;

        /// <summary>
        /// The maximum time to wait for a client to continue transmitting a
        /// request.  Specify <b>0</b> for effectively unlimited (actually 7 days).
        /// This defaults to  <b>50 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultClientSeconds)]
        public double ClientSeconds { get; set; } = DefaultClientSeconds;

        /// <summary>
        /// The maximum time to wait for a server to acknowledge or transmit
        /// data.  Specify <b>0</b> for effectively unlimited (actually 7 days).
        /// This defaults to  <b>50 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ServerSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultServerSeconds)]
        public double ServerSeconds { get; set; } = DefaultServerSeconds;

        /// <summary>
        /// The maximum time to wait for a health check.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultCheckSeconsds)]
        public double CheckSeconds { get; set; } = DefaultCheckSeconsds;

        /// <summary>
        /// The maximum time to keep a client side HTTP connection open after
        /// returning the first response to wait for a another client request.
        /// Specify <b>0</b> for essentually unlimited (actually 7 days).
        /// This defaults to <b>50 seconds</b>.  This defaults to <b>0.5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HttpKeepAliveSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DefaultClientHttpKeepAliveSeconds)]
        public double HttpKeepAliveSeconds { get; set; } = DefaultClientHttpKeepAliveSeconds;

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(TrafficValidationContext context)
        {
            if (ConnectSeconds < 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ConnectSeconds)}={ConnectSeconds}] is not valid.");
            }

            if (ClientSeconds < 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ClientSeconds)}={ClientSeconds}] is not valid.");
            }

            if (HttpKeepAliveSeconds < 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(HttpKeepAliveSeconds)}={HttpKeepAliveSeconds}] is not valid.");
            }

            if (ServerSeconds < 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(ServerSeconds)}={ServerSeconds}] is not valid.");
            }

            if (CheckSeconds <= 0.0)
            {
                context.Error($"Load balancer timeout [{nameof(CheckSeconds)}={CheckSeconds}] is not positive.");
            }
        }
    }
}
