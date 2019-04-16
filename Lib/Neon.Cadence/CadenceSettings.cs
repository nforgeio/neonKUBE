//-----------------------------------------------------------------------------
// FILE:	    CadenceSettings.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Cadence client settings.
    /// </summary>
    public class CadenceSettings
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CadenceSettings()
        {
        }

        /// <summary>
        /// Specifies rhe connection mode.  User applications should use
        /// the default: <see cref="ConnectionMode.Normal"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Mode", ApplyNamingConventions = false)]
        [DefaultValue(ConnectionMode.Normal)]
        public ConnectionMode Mode { get; set; } = ConnectionMode.Normal;

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
        [YamlMember(Alias = "Servers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();

        /// <summary>
        /// The port where the client will listen for traffic from the associated Cadency Proxy.
        /// This default to 0 which specifies that we'll let the operating system choose an
        /// unused ephermal port.
        /// </summary>
        public int ListenPort { get; set; }
    }
}
