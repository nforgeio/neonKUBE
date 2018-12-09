//-----------------------------------------------------------------------------
// FILE:	    TrafficFrontend.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
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
    /// Base class for traffic manager frontends. 
    /// </summary>
    public class TrafficFrontend
    {
        /// <summary>
        /// The maximum number of connections to be allowed for this
        /// frontend or zero if the number of connections will be limited
        /// to the overall pool of connections specified by <see cref="TrafficSettings.MaxConnections"/>.
        /// This defaults to <b>0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MaxConnections { get; set; } = 0;

        /// <summary>
        /// Validates the frontend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficRule rule)
        {
            // Verify [MaxConnections]

            if (MaxConnections < 0)
            {
                context.Error($"Rule [{rule.Name}] specifies invalid [{nameof(MaxConnections)}={MaxConnections}].");
            }
        }
    }
}
