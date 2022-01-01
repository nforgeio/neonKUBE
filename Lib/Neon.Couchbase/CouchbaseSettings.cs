//-----------------------------------------------------------------------------
// FILE:	    CouchbaseSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using Neon.Common;

using Couchbase;
using Couchbase.N1QL;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

namespace Neon.Data
{
    /// <summary>
    /// Settings used to connect a Couchbase client to a Couchbase bucket.
    /// </summary>
    public class CouchbaseSettings
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parses a <see cref="CouchbaseSettings"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml">The JSON or YAML text.</param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed <see cref="CouchbaseSettings"/>.</returns>
        public static CouchbaseSettings Parse(string jsonOrYaml, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml), nameof(jsonOrYaml));

            return NeonHelper.JsonOrYamlDeserialize<CouchbaseSettings>(jsonOrYaml, strict);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CouchbaseSettings()
        {
        }

        /// <summary>
        /// Constructs an instance with server URIs.
        /// </summary>
        /// <param name="servers">Specifies one or more server URIs.</param>
        public CouchbaseSettings(params Uri[] servers)
        {
            foreach (var server in servers)
            {
                this.Servers.Add(server);
            }
        }

        /// <summary>
        /// One or more Couchbase server URIs.
        /// </summary>
        /// <remarks>
        /// You must specify the URI for at least one operating Couchbase node.  The Couchbase
        /// client will use this to discover the remaining nodes.  It is a best practice to
        /// specify multiple nodes in a clustered environment to avoid initial connection
        /// problems if any single node is down.
        /// </remarks>
        [JsonProperty(PropertyName = "Servers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "servers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();

        /// <summary>
        /// Optionally specifies the name of the target Couchbase bucket.
        /// </summary>
        [JsonProperty(PropertyName = "Bucket", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "bucket", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Bucket { get; set; }

        /// <summary>
        /// Maximum time (milliseconds) to wait to establish a server connection (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "ConnectTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "connectTimeout", ApplyNamingConventions = false)]
        [DefaultValue(10000)]
        public int ConnectTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait to transmit a server request (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "SendTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sendTimeout", ApplyNamingConventions = false)]
        [DefaultValue(10000)]
        public int SendTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait for an operation to complete (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "OperationTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "operationTimeout", ApplyNamingConventions = false)]
        [DefaultValue(10000)]
        public int OperationTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait for a non-view query to complete (defaults to 75 seconds).
        /// </summary>
        [JsonProperty(PropertyName = "QueryRequestTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "queryRequestTimeout", ApplyNamingConventions = false)]
        [DefaultValue(75000)]
        public int QueryRequestTimeout { get; set; } = 75000;

        /// <summary>
        /// Maximum time (milliseconds) to wait for a view query to complete (defaults to 75 seconds).
        /// </summary>
        [JsonProperty(PropertyName = "ViewRequestTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "viewRequestTimeout", ApplyNamingConventions = false)]
        [DefaultValue(75000)]
        public int ViewRequestTimeout { get; set; } = 75000;

        /// <summary>
        /// Maximum number of pooled connections to a server bucket (defaults to <b>5</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MaxPoolConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "maxPoolConnections", ApplyNamingConventions = false)]
        [DefaultValue(5)]
        public int MaxPoolConnections { get; set; } = 5;

        /// <summary>
        /// Minimum number of pooled connections to a server bucket (defaults to <b>2</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MinPoolConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "minPoolConnections", ApplyNamingConventions = false)]
        [DefaultValue(2)]
        public int MinPoolConnections { get; set; } = 2;

        /// <summary>
        /// Enables the use of the <see cref="ScanConsistency.RequestPlus"/> index consistency option.  
        /// Both of these options are deprecated as of Couchbase 5.0 so this may no longer matter.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "UseEnhancedDurability", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "useEnhancedDurability", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool UseEnhancedDurability { get; set; } = true;

        /// <summary>
        /// Returns <c>true</c> if the settings are valid.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsValid
        {
            get
            {
                if (Servers == null || Servers.Count == 0)
                {
                    return false;
                }

                foreach (var server in Servers)
                {
                    if (server == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
