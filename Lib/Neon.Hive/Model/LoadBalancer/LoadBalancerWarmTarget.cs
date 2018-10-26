//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerWarmTarget.cs
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
    /// <remarks>
    /// <para>
    /// 
    /// </para>
    /// </remarks>
    public class LoadBalancerWarmTarget
    {
        private const string defaultUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36";
        private const string defaultMethod    = "GET";

        /// <summary>
        /// <para>
        /// The fully qualified URI of the resource to be proactively loaded
        /// into the cache.  This is required.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> The URI <b>scheme</b>, <b>hostname</b>, and <b>port</b> must 
        /// match one of the load balancer rule frontends.
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
        /// The HTTP method to be used when retrieving the target.  This defaults
        /// to <b>GET</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Method", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMethod)]
        public string Method { get; set; } = defaultMethod;

        /// <summary>
        /// Optionally specifies the <b>Content-Type</b> header to be used
        /// when retrieving the target when <see cref="Content"/> is also
        /// specified.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "ContentType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// Specifies the content text to be included in the request made 
        /// when retrieving the target.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Content", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Content { get; set; } = null;

        /// <summary>
        /// Validates the item.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(LoadBalancerValidationContext context)
        {
            Method = Method ?? defaultMethod;

            if (string.IsNullOrEmpty(Uri))
            {
                context.Error($"[{nameof(LoadBalancerWarmTarget)}.{nameof(Uri)}] cannot be NULL or empty.");
            }

            if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out var uri))
            {
                context.Error($"[{nameof(LoadBalancerWarmTarget)}.{nameof(Uri)}={Uri}] is not a valid fully qualified URI.");
            }

            if (uri.Scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase))
            {
                context.Error($"HTTPS backend caching is not supported: [{nameof(LoadBalancerWarmTarget)}.{nameof(Uri)}={Uri}]");
            }
        }
    }
}
