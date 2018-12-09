//-----------------------------------------------------------------------------
// FILE:	    TrafficHttpBackend.cs
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
    /// Describes an HTTP/HTTPS traffic manager backend.
    /// </summary>
    public class TrafficHttpBackend : TrafficBackend
    {
        /// <summary>
        /// Forward the request to this backend using TLS (defaults to <c>false</c>).
        /// </summary>
        [JsonProperty(PropertyName = "Tls", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Tls { get; set; } = false;

        /// <summary>
        /// Validates the backend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficHttpRule rule)
        {
            base.Validate(context, rule.Name);
        }
    }
}
