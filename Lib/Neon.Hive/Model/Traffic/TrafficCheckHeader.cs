//-----------------------------------------------------------------------------
// FILE:	    TrafficCheckHeader.cs
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
using Neon.Cryptography;

namespace Neon.Hive
{
    /// <summary>
    /// Describes an HTTP header to be included with an HTTP health
    /// check submitted to a load balanced backend.
    /// </summary>
    public class TrafficCheckHeader
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public TrafficCheckHeader()
        {
        }

        /// <summary>
        /// Constructs an instance from a header name and value.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        public TrafficCheckHeader(string name, string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(name));
            Covenant.Requires<ArgumentNullException>(value != null);
        }

        /// <summary>
        /// The header name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The header value.
        /// </summary>
        [JsonProperty(PropertyName = "Value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Value { get; set; }

        /// <summary>
        /// Validates the header.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficRule rule)
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                context.Error($"Rule [{rule.Name}] specifies a NULL or empty [{nameof(TrafficCheckHeader)}.{nameof(TrafficCheckHeader.Name)}].");
            }

            foreach (var ch in Name)
            {
                if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_')
                {
                    continue;
                }

                context.Error($"Rule [{rule.Name}] specifies a [{nameof(TrafficCheckHeader)}.{nameof(TrafficCheckHeader.Name)}] with the invalid character [{ch}].");
                break;
            }

            if (string.IsNullOrWhiteSpace(Value))
            {
                context.Error($"Rule [{rule.Name}] specifies a NULL [{nameof(TrafficCheckHeader)}.{nameof(TrafficCheckHeader.Value)}].");
            }

            // $todo(jeff.lill):
            //
            // We could try to validate the [Value] property too (e.g. to ensure that it doesn't include "\r\n") but
            // that could be overly restrictive if I'm not careful.  I'm going to leave this be for now.
        }
    }
}
