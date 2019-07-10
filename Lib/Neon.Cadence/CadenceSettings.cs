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
using Neon.Cadence.Internal;

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
        public List<string> Servers { get; set; } = new List<string>();

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
        /// Specifies the default Cadence domain for this client.  This is required.
        /// </summary>
        [JsonProperty(PropertyName = "DefaultDomain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "defaultDomain", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DefaultDomain { get; set; }

        /// <summary>
        /// Optionally create the <see cref="DefaultDomain"/> if it doesn't already exist.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "CreateDomain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "createDomain", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool CreateDomain { get; set; } = false;

        /// <summary>
        /// Optionally specifies the maximum time the client should wait for synchronous 
        /// operations to complete.  This defaults to <b>45 seconds</b> when not set.
        /// </summary>
        [JsonProperty(PropertyName = "ClientTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clientTimeout", ApplyNamingConventions = false)]
        [DefaultValue(45.0)]
        public double ClientTimeoutSeconds { get; set; } = 45.0;

        /// <summary>
        /// Returns <see cref="ClientTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ClientTimeout => TimeSpan.FromSeconds(ClientTimeoutSeconds);

        /// <summary>
        /// Optionally identifies the client application establishing the connection so that
        /// Cadence may include this in its logs and metrics.  This defaults to <b>"unknown"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ClientIdentity", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clientIdentity", ApplyNamingConventions = false)]
        [DefaultValue("unknown")]
        public string ClientIdentity { get; set; } = "unknown";

        /// <summary>
        /// Optionally specifies the maximum time to allow the <b>cadence-proxy</b>
        /// to indicate that it has received a proxy request message by returning an
        /// OK response.  The proxy will be considered to be unhealthy when this 
        /// happens.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "proxyTimeout", ApplyNamingConventions = false)]
        [DefaultValue(5.0)]
        public double ProxyTimeoutSeconds { get; set; } = 5.0;

        /// <summary>
        /// Returns <see cref="ProxyTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ProxyTimeout => TimeSpan.FromSeconds(ProxyTimeoutSeconds);

        /// <summary>
        /// Optionally specifies the maximum time to allow the <b>cadence-proxy</b>
        /// to gracefully close its Cadence cluster connection and terminate.  The proxy
        /// will be forceably killed when this time is exceeded.  This defaults to
        /// <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "TerminateTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "terminateTimeout", ApplyNamingConventions = false)]
        [DefaultValue(0.0)]
        public double TerminateTimeoutSeconds { get; set; } = 10.0;

        /// <summary>
        /// Returns <see cref="TerminateTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan TerminateTimeout => TimeSpan.FromSeconds(Math.Max(TerminateTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the number of times to retry connecting to the Cadence cluster.  This defaults
        /// to <b>3</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ConnectRetries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "connectRetries", ApplyNamingConventions = false)]
        [DefaultValue(3)]
        public int ConnectRetries { get; set; } = 3;

        /// <summary>
        /// Specifies the number of seconds to delay between cluster connection attempts.
        /// This defaults to <b>5.0 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ConnectRetryDelaySeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "connectRetryDelaySeconds", ApplyNamingConventions = false)]
        [DefaultValue(5.0)]
        public double ConnectRetryDelaySeconds { get; set; } = 5.0;

        /// <summary>
        /// Returns <see cref="ConnectRetryDelaySeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ConnectRetryDelay => TimeSpan.FromSeconds(Math.Max(ConnectRetryDelaySeconds, 0));

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
        /// <note>
        /// <b>IMPORTANT:</b> Eventually, we'd like to implement a high-fidelity in-memory emulation
        /// mode for user based unit testing but the library isn't there yet.  We don't recommend
        /// that you enable emulation at this time.
        /// </note>
        /// <para>
        /// Optionally specifies that a local in-memory Cadence emulation should be started
        /// for unit testing.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "Emulate", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "emulate", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Emulate { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-proxy</b> will
        /// already be running for debugging purposes.  When this is <c>true</c>, the 
        /// <b>cadence-client</b> be hardcoded to listen on <b>127.0.0.2:5001</b> and
        /// the <b>cadence-proxy</b> will be assumed to be listening on <b>127.0.0.2:5000</b>.
        /// This defaults to <c>false.</c>
        /// </summary>
        internal bool DebugPrelaunched { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-client</b>
        /// will not perform the <see cref="InitializeRequest"/>/<see cref="InitializeReply"/>
        /// and <see cref="TerminateRequest"/>/<see cref="TerminateReply"/> handshakes 
        /// with the <b>cadence-proxy</b> for debugging purposes.  This defaults to
        /// <c>false</c>.
        /// </summary>
        internal bool DebugDisableHandshakes { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally disable health heartbeats.  This can be
        /// useful while debugging the client but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        internal bool DebugDisableHeartbeats { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally ignore operation timeouts.  This can be
        /// useful while debugging the client but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        internal bool DebugIgnoreTimeouts { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally disables heartbeat handling by the
        /// emulated <b>cadence-proxy</b> for testing purposes.
        /// </summary>
        internal bool DebugIgnoreHeartbeats { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally specifies the timeout to use for 
        /// HTTP requests made to the <b>cadence-proxy</b>.  This defaults to
        /// <b>5 seconds</b>.
        /// </summary>
        internal TimeSpan DebugHttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
