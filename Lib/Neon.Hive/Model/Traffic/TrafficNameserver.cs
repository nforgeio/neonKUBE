//-----------------------------------------------------------------------------
// FILE:	    TrafficNameserver.cs
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
    /// Describes a traffic manager DNS resolver nameserver.
    /// </summary>
    public class TrafficNameserver
    {
        /// <summary>
        /// Unique label for the nameserver.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// IP endpoint (<b>address</b>:<b>port</b>) of the nameserver.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoint", Required = Required.Always)]
        public string Endpoint { get; set; }

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(TrafficValidationContext context)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                context.Error("Load balancer nameserver name cannot be null or empty.");
            }

            var isValid = false;

            if (!string.IsNullOrWhiteSpace(Endpoint))
            {
                var colonPos = Endpoint.LastIndexOf(':');

                if (colonPos >= 0)
                {
                    var addressPart = Endpoint.Substring(0, colonPos);
                    var portPart    = Endpoint.Substring(colonPos + 1);

                    ushort      port;

                    isValid = IPAddress.TryParse(addressPart, out var address) && ushort.TryParse(portPart, out port);
                }

                if (!isValid)
                {
                    context.Error($"[{nameof(TrafficNameserver)}.{nameof(Name)}={Endpoint}] is not valid.");
                }
            }
        }
    }
}
