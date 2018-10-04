//-----------------------------------------------------------------------------
// FILE:	    VarnishOptions.cs
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
    /// Specifies the options for configuring the hive <a href="https://varnish-cache.org/">Varnish HTTP Cache</a>.
    /// </summary>
    public class VarnishOptions
    {
        private const string defaultCacheSize = "100MB";

        /// <summary>
        /// Specifies the size of the varnish cache.  This can be a byte count or a number with units like
        /// <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CacheSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultCacheSize)]
        public string CacheSize { get; set; } = defaultCacheSize;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
        }
    }
}
