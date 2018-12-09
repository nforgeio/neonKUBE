//-----------------------------------------------------------------------------
// FILE:	    TrafficWarmTarget.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Neon.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neon.Hive
{
    /// <summary>
    /// Defines the URL and other settings to be used for periodically having 
    /// the <b>neon-proxy-public-cache</b> or <b>neon-proxy-private-cache</b> 
    /// services proactively pulls content into the cache.
    /// </summary>
    public class TrafficWarmTarget
    {
        private const string defaultUserAgent     = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
        private const double defaultUpdateSeconds = 60;

        /// <summary>
        /// <para>
        /// The fully qualified URI of the resource to be proactively loaded
        /// into the cache.  This is required.
        /// </para>
        /// <note>
        /// The URI <b>scheme</b>, <b>hostname</b>, and <b>port</b> must map to
        /// one of the parent <see cref="TrafficHttpRule"/> frontends.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Uri", Required = Required.Always)]
        public string Uri { get; set; }

        /// <summary>
        /// The <b>User-Agent</b> header to be included in the proactive caching request.
        /// This defaults to specifying a recent Google Chrome user agent running on
        /// 64-bit Windows.
        /// </summary>
        [JsonProperty(PropertyName = "UserAgent", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultUserAgent)]
        public string UserAgent { get; set; } = defaultUserAgent;

        /// <summary>
        /// The update interval in seconds.  This defaults to <b>60 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "UpdateSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultUpdateSeconds)]
        public double UpdateSeconds { get; set; } = defaultUpdateSeconds;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> The value to be passed as the <b>X-Neon-Frontend</b> header when
        /// fetching the target through the Varnish cache.  This is set and used internally by the
        /// <b>neon-proxy-manager</b> and <b>neon-proxy-cache</b> based services.
        /// </summary>
        [JsonProperty(PropertyName = "FrontendHeader", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string FrontendHeader { get; set; }
            
        /// <summary>
        /// Validates the item.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(TrafficValidationContext context)
        {
            if (string.IsNullOrEmpty(Uri))
            {
                context.Error($"[{nameof(TrafficWarmTarget)}.{nameof(Uri)}] cannot be NULL or empty.");
            }

            if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var uri))
            {
                context.Error($"[{nameof(TrafficWarmTarget)}.{nameof(Uri)}={Uri}] is not a valid fully qualified URI.");
            }

            if (UpdateSeconds <= 0)
            {
                UpdateSeconds = defaultUpdateSeconds;
            }
        }
    }
}
