//-----------------------------------------------------------------------------
// FILE:	    DatabaseConfiguration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Configuration information for a Sync Gateway database.
    /// </summary>
    public class DatabaseConfiguration
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DatabaseConfiguration()
        {
        }

        /// <summary>
        /// The database name.
        /// </summary>
        [JsonProperty(PropertyName = "Name")]
        [YamlMember(Alias = "Name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The database server REST URI including the port (typically 8091).
        /// </summary>
        [JsonProperty(PropertyName = "Server")]
        [YamlMember(Alias = "Server", ApplyNamingConventions = false)]
        public string Server { get; set; }

        /// <summary>
        /// The data bucket name.
        /// </summary>
        [JsonProperty(PropertyName = "Bucket")]
        [YamlMember(Alias = "Bucket", ApplyNamingConventions = false)]
        public string Bucket { get; set; }

        /// <summary>
        /// The Javascript <b>sync</b> function code.
        /// </summary>
        [JsonProperty(PropertyName = "Sync")]
        [YamlMember(Alias = "Sync", ApplyNamingConventions = false)]
        public string Sync { get; set; }
    }
}
