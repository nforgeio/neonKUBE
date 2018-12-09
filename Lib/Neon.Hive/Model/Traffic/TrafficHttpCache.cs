//-----------------------------------------------------------------------------
// FILE:	    TrafficHttpCache.cs
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
using Neon.Cryptography;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// HTTP caching related settings.
    /// </summary>
    public class TrafficHttpCache
    {
        private const int defaultWarmSeconds = 120;

        /// <summary>
        /// Enables HTTP caching for a <see cref="TrafficHttpRule"/>.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// <para>
        /// Enables DEBUG mode which currently will add the <c>X-Neon-Cache: name=value[,name=value...]</c>
        /// header to all responses that come from <b>neon-proxy-cache-public</b> and <b>neon-proxy-cache-private</b>
        /// for the associated traffic manager rule when caching is enabled.  Details will be returned as one
        /// or more comma separated <c>name=value</c> items.  Currently, only <b>hits=HIT-COUNT</b> is
        /// returned but additional information may be returned in the future.
        /// </para>
        /// <para>
        /// <b>hits</b> is the number of times object returned has been served from the cache.  Here's 
        /// how this can be useful:
        /// </para>
        /// <list type="bullet">
        /// <item>
        /// The presense of this header indicates that traffic is passing through the cache.
        /// </item>
        /// <item>
        /// <b>hits > 0</b> indicates that the response was served from the cache with the
        /// count being the number of times a cached response was returned for the URL.
        /// </item>
        /// <item>
        /// <para>
        /// <b>hits = 0</b> indicates that the response was not served from the cache.
        /// Here are the reasons why this happens:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     The origin server added headers that disables caching for the URL.
        ///     </item>
        ///     <item>
        ///     This is the first time a cachable object has been retrieved.  The
        ///     first request will return <b>HIT-COUNT=0</b> but subsequent
        ///     requests will return positive hit counts until the item expires
        ///     or is otherwise removed.
        ///     </item>
        ///     <item>
        ///     The item has been purged or banned.
        ///     </item>
        ///     <item>
        ///     The item is not allowed to be cached for some other reason (e.g. it's too large).
        ///     </item>
        ///     </list>
        ///    </item>
        /// </list>
        /// <para>
        /// <see cref="Debug"/> defaults to <c>false</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Optionally overrides the TTL for DNS lookups performed by the cache when the
        /// backend specifies a hostname (as opposed to an IP address) for the origin server.
        /// This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "DnsTTL", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5)]
        public int DnsTTL { get; set; } = 5;

        /// <summary>
        /// Optionally specifies the resources that should be proactively loaded into the cache.
        /// Each target specifies the URI of the resource to be cached and other settings such as 
        /// the update interval and the <b>User-Agent</b> to be mimicked when making the requests.
        /// </summary>
        [JsonProperty(PropertyName = "WarmTargets", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<TrafficWarmTarget> WarmTargets { get; set; } = new List<TrafficWarmTarget>();

        /// <summary>
        /// Validates the settings.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficHttpRule rule)
        {
            if (DnsTTL < 1)
            {
                context.Error($"[{nameof(TrafficHttpCache)}.{nameof(DnsTTL)}={DnsTTL}] cannot be less than 1 second.");
            }

            WarmTargets = WarmTargets ?? new List<TrafficWarmTarget>();

            // Verify that each warm target has valid properties and that they
            // all match at one of the rule frontends.

            foreach (var target in WarmTargets)
            {
                target.Validate(context);

                if (rule.GetFrontendForWarmTarget(target) == null)
                {
                    context.Error($"Rule [{rule.Name}] includes the [{target.Uri}] cache warming target which cannot be mapped to a rule frontend.");
                }
            }
        }
    }
}
