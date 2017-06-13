//-----------------------------------------------------------------------------
// FILE:	    DbCouchbaseConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Couchbase client configuration settings.  These are persisted in Consul as
    /// the <see cref="DbClusterInfo.ClientConfig"/> property (as JSON text).
    /// </summary>
    public class DbCouchbaseConfig
    {
        /// <summary>
        /// The default and sets the Views REST API to use a custom port.
        /// </summary>
        [JsonProperty(PropertyName = "ApiPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(8092)]
        public int ApiPort { get; set; } = 8092;

        /// <summary>
        /// The default and sets the direct port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        [JsonProperty(PropertyName = "DirectPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(11210)]
        public int DirectPort { get; set; } = 11210;

        /// <summary>
        /// The default and sets the Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        [JsonProperty(PropertyName = "HttpsApiPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(18092)]
        public int HttpsApiPort { get; set; } = 18092;

        /// <summary>
        /// The default and sets the Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        [JsonProperty(PropertyName = "HttpsMgmtPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(18091)]
        public int HttpsMgmtPort { get; set; } = 18091;

        /// <summary>
        /// The default and sets the Couchbase Management REST API to use a custom port.
        /// </summary>
        [JsonProperty(PropertyName = "MgmtPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(8091)]
        public int MgmtPort { get; set; } = 8091;

        /// <summary>
        /// The default and sets the SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        [JsonProperty(PropertyName = "SslPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(11207)]
        public int SslPort { get; set; } = 11207;

        /// <summary>
        /// Identifies the client bootstrap servers.
        /// </summary>
        [JsonProperty(PropertyName = "Servers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();
    }
}
