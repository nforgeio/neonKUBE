//-----------------------------------------------------------------------------
// FILE:	    DbCouchbaseSettings.cs
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
    /// the <see cref="DbClusterInfo.ClientSettings"/> property (as JSON text).
    /// </summary>
    public class DbCouchbaseSettings
    {
        /// <summary>
        /// The Views REST API to use a custom port.
        /// </summary>
        [JsonProperty(PropertyName = "ApiPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int ApiPort { get; set; } = 8092;

        /// <summary>
        /// The direct port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        [JsonProperty(PropertyName = "DirectPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int DirectPort { get; set; } = 11210;

        /// <summary>
        /// The Couchbase query services port.
        /// </summary>
        [JsonProperty(PropertyName = "QueryPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int QueryPort { get; set; } = 8093;

        /// <summary>
        /// The Couchbase search port.
        /// </summary>
        [JsonProperty(PropertyName = "SearchPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int SearchPort { get; set; } = 8094;

        /// <summary>
        /// The Couchbase Views REST API to use a custom SSL port.
        /// </summary>
        [JsonProperty(PropertyName = "HttpsApiPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int HttpsApiPort { get; set; } = 18092;

        /// <summary>
        /// The Couchbase Management REST API to use a custom SSL port.
        /// </summary>
        [JsonProperty(PropertyName = "HttpsMgmtPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int HttpsMgmtPort { get; set; } = 18091;

        /// <summary>
        /// The Couchbase SSL query services port.
        /// </summary>
        [JsonProperty(PropertyName = "HttpsQueryPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int HttpsQueryPort { get; set; } = 18093;

        /// <summary>
        /// The Couchbase Management REST API to use a custom port.
        /// </summary>
        [JsonProperty(PropertyName = "MgmtPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int MgmtPort { get; set; } = 8091;

        /// <summary>
        /// The SSL port to use for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        [JsonProperty(PropertyName = "SslPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int SslPort { get; set; } = 11207;

        /// <summary>
        /// Indicates whether SSL should be used for Key/Value operations using the Binary Memcached protocol.
        /// </summary>
        [JsonProperty(PropertyName = "UseSsl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool UseSsl { get; set; } = false;

        /// <summary>
        /// Identifies the client bootstrap servers.
        /// </summary>
        [JsonProperty(PropertyName = "Servers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();
    }
}
