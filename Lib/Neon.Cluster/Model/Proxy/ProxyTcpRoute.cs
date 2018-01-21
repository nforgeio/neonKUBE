//-----------------------------------------------------------------------------
// FILE:	    ProxyTcpRoute.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a route that forwards TCP traffic from TCP frontends
    /// to TCP backends.
    /// </summary>
    public class ProxyTcpRoute : ProxyRoute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyTcpRoute()
        {
            Mode = ProxyMode.Tcp;
        }

        /// <summary>
        /// The proxy frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<ProxyTcpFrontend> Frontends = new List<ProxyTcpFrontend>();

        /// <summary>
        /// The proxy backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<ProxyTcpBackend> Backends = new List<ProxyTcpBackend>();

        /// <summary>
        /// The maximum overall number of connections to be allowed for this
        /// route or zero if the number of connections will be limited
        /// to the overall pool of connections specified by <see cref="ProxySettings.MaxConnections"/>.
        /// This defaults to <b>0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConnections { get; set; } = 0;

        /// <summary>
        /// Validates the route.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public override void Validate(ProxyValidationContext context)
        {
            base.Validate(context);

            foreach (var frontend in Frontends)
            {
                frontend.Validate(context, this);
            }

            foreach (var backend in Backends)
            {
                backend.Validate(context, this);
            }

            // Verify that the ports are unique for each frontend.

            var frontendMap = new HashSet<int>();

            foreach (var frontend in Frontends)
            {
                var key = frontend.ProxyPort;

                if (frontendMap.Contains(key))
                {
                    context.Error($"TCP route [{Name}] includes two or more frontends that map to port [{key}].");
                }

                frontendMap.Add(key);
            }

            if (MaxConnections < 0 || MaxConnections > ushort.MaxValue)
            {
                context.Error($"Route [{Name}] specifies invalid [{nameof(MaxConnections)}={MaxConnections}].");
            }
        }
    }
}
