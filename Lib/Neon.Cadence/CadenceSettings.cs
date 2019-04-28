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
        /// Specifies the connection mode.  User applications should use
        /// the default: <see cref="ConnectionMode.Normal"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "mode", ApplyNamingConventions = false)]
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
        [YamlMember(Alias = "servers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();

        /// <summary>
        /// Optionally specifies the port where the client will listen for traffic from the 
        /// associated Cadency Proxy.  This defaults to 0 which specifies that lets the 
        /// operating system choose an unused ephermal port.
        /// </summary>
        [JsonProperty(PropertyName = "ListenPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "listenPort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ListenPort { get; set; } = 0;

        /// <summary>
        /// Optionally specifies the maximum time to allow the <b>cadence-proxy</b>
        /// to indicate that it has received a proxy request message by returning an
        /// OK response.  The proxy will be considered to be unhealthy when this 
        /// happens.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "proxyTimeout", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public TimeSpan ProxyTimeout { get; set; } = default;

        /// <summary>
        /// Optionally specifies the folder where the embedded <b>cadence-proxy</b> binary 
        /// will be written before starting it.  This defaults to <c>null</c> which specifies
        /// that the binary will be written to the same folder where the <b>Neon.Cadence</b>
        /// assembly resides.  This folder may not be writable by the current user so this
        /// allows you to specify an alternative folder.
        /// </summary>
        [JsonProperty(PropertyName = "BinaryFolder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "binaryFolder", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string BinaryFolder { get; set; } = null;

        /// <summary>
        /// Optionally specifies the logging level for the associated <b>cadence-proxy</b>.
        /// The supported values are <b>panic</b>, <b>fatal</b>, <b>error</b>, <b>warn</b>, 
        /// and <b>debug</b>.  This defaults to <b>info</b>.
        /// </summary>
        [JsonProperty(PropertyName = "LogLevel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logLevel", ApplyNamingConventions = false)]
        [DefaultValue("info")]
        public string LogLevel { get; set; } = "info";

        /// <summary>
        /// Optionally specifies that the connection should run in DEBUG mode.  This currently
        /// launches the <b>cadence-proxy</b> with a command window (on Windows only) to make 
        /// it easy to see any output it generates and also has <b>cadence-proxy</b>.  This
        /// defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "debug", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally specifies that the real <b>cadence-proxy</b>
        /// should not be started and a partially implemented local emulation should be started 
        /// in its place.  This is used internally for low-level testing and should never be 
        /// enabled for production (because it won't work).
        /// </summary>
        internal bool EmulateProxy { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally disable health heartbeats.  This can be
        /// useful while debugging the library but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        internal bool DisableHeartbeats { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally ignore operation timeouts.  This can be
        /// useful while debugging the library but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        internal bool IgnoreTimeouts { get; set; } = false;
    }
}
