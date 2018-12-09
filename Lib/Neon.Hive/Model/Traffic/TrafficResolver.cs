//-----------------------------------------------------------------------------
// FILE:	    TrafficResolver.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Describes a traffic manager DNS resolver.
    /// </summary>
    public class TrafficResolver
    {
        /// <summary>
        /// The unique name for the resolver.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The resolver's name servers.
        /// </summary>
        [JsonProperty(PropertyName = "NameServers", Required = Required.Always)]
        public List<TrafficNameserver> NameServers { get; set; } = new List<TrafficNameserver>();

        /// <summary>
        /// The number of times to retry a failed resolution (defaults to <b>3</b>).
        /// </summary>
        [JsonProperty(PropertyName = "ResolveRetries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(3)]
        public int ResolveRetries { get; set; } = 3;

        /// <summary>
        /// The seconds to wait before retrying a resolution (defaults to <b>1 second</b>).
        /// </summary>
        [JsonProperty(PropertyName = "RetrySeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1.0)]
        public double RetrySeconds { get; set; } = 1.0;

        /// <summary>
        /// The number of seconds to hold a successful resolution (defaults to <b>5 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "HoldSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5.0)]
        public double HoldSeconds { get; set; } = 5.0;

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(TrafficValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                context.Error($"Load balancer resolver [{nameof(Name)}] cannot be null or empty.");
            }

            if (NameServers == null || NameServers.Count == 0)
            {
                context.Error($"Load balancer resolver [{nameof(NameServers)}] at least one name server must be specified.");
            }

            if (ResolveRetries < 0)
            {
                context.Error($"Load balancer resolver [{nameof(ResolveRetries)}={ResolveRetries}] is not valid.");
            }

            if (RetrySeconds <= 0.0)
            {
                context.Error($"Load balancer resolver [{nameof(RetrySeconds)}={RetrySeconds}] is not valid.");
            }

            if (HoldSeconds < 0.0)
            {
                context.Error($"Load balancer resolver [{nameof(HoldSeconds)}={HoldSeconds}] is not valid.");
            }
        }
    }
}
