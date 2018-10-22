//-----------------------------------------------------------------------------
// FILE:	    ProxyOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the options for configuring the hive's built-in <a href="http://haproxy.org">HAProxy</a>
    /// <a href="https://varnish-cache.org/">Varnish HTTP Cache</a> related services.
    /// </summary>
    public class ProxyOptions
    {
        private const string defaultCacheSize = "100MB";

        /// <summary>
        /// Specifies the number of <b>neon-proxy-public-cache</b> replicas to be deployed.
        /// This defaults to <b>1</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicCacheReplicas", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1)]
        public int PublicCacheReplicas { get; set; } = 1;

        /// <summary>
        /// Specifies the size of the cache for each <b>neon-proxy-public-cache</b> instance.  This
        /// can be a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, 
        /// or <b>1TB</b>.  This defaults to <b>100MB</b> and cannot be less than <b>50MB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicCacheSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultCacheSize)]
        public string PublicCacheSize { get; set; } = defaultCacheSize;

        /// <summary>
        /// Specifies the scheduling constraints for the <b>neon-proxy-public-cache</b> service
        /// instances.  This defaults to <c>null</c> which case neonHIVE setup will attempt to do something
        /// reasonable.  See the remarks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this property is <c>null</c> or empty, neonHIVE setup do something reasonable: 
        /// </para>
        /// <list type="number">
        /// <item>
        /// If the hive has at least <see cref="PublicCacheReplicas"/> workers then neonHIVE will
        /// use the <b>node.role==worker</b>,
        /// </item>
        /// <item>
        /// When there aren't enough workers then a constraint won't be imposed so the cache instances
        /// can be scheduled on both managers and workers.
        /// </item>
        /// </list>
        /// <note>
        /// By default, we're going to try to keep the cache instances off of the hive managers
        /// as a best practice.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicCacheConstraint", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PublicCacheConstraint { get; set; } = null;

        /// <summary>
        /// Specifies the number of <b>neon-proxy-private-cache</b> replicas to be deployed.
        /// This defaults to <b>1</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateCacheReplicas", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(1)]
        public int PrivateCacheReplicas { get; set; } = 1;

        /// <summary>
        /// Specifies the size of the cache for each <b>neon-proxy-private-cache</b> instance.  This
        /// can be a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, 
        /// or <b>1TB</b>.  This defaults to <b>100MB</b> and cannot be less than <b>50MB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateCacheSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultCacheSize)]
        public string PrivateCacheSize { get; set; } = defaultCacheSize;

        /// <summary>
        /// Specifies the scheduling constraints for the <b>neon-proxy-private-cache</b> service
        /// instances.  This defaults to <c>null</c> which case neonHIVE setup will attempt to do something
        /// reasonable.  See the remarks.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When this property is <c>null</c> or empty, neonHIVE setup do something reasonable: 
        /// </para>
        /// <list type="number">
        /// <item>
        /// If the hive has at least <see cref="PrivateCacheReplicas"/> workers then neonHIVE will
        /// use the <b>node.role==worker</b>,
        /// </item>
        /// <item>
        /// When there aren't enough workers then a constraint won't be imposed so the cache instances
        /// can be scheduled on both managers and workers.
        /// </item>
        /// </list>
        /// <note>
        /// By default, we're going to try to keep the cache instances off of the hive managers
        /// as a best practice.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PrivateCacheConstraint", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PrivateCacheConstraint { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
            if (PublicCacheReplicas <= 0)
            {
                throw new HiveDefinitionException($"[{nameof(ProxyOptions)}.{nameof(PublicCacheReplicas)}={PublicCacheReplicas}] must be at least [1].");
            }

            if (HiveDefinition.ValidateSize(PublicCacheSize, this.GetType(), nameof(PublicCacheSize)) < 50 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(PublicCacheSize)}={PublicCacheSize}] cannot be less than [50MB].");
            }

            if (PrivateCacheReplicas <= 0)
            {
                throw new HiveDefinitionException($"[{nameof(ProxyOptions)}.{nameof(PrivateCacheReplicas)}={PrivateCacheReplicas}] must be at least [1].");
            }

            if (HiveDefinition.ValidateSize(PrivateCacheSize, this.GetType(), nameof(PrivateCacheSize)) < 50 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(PrivateCacheSize)}={PrivateCacheSize}] cannot be less than [50MB].");
            }
        }
    }
}
