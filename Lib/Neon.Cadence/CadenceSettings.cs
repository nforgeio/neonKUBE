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

using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Cadence
{
    /// <summary>
    /// Cadence client settings.
    /// </summary>
    public class CadenceSettings
    {
        private const double defaultTimeoutSeconds = 24 * 3600;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CadenceSettings()
        {
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
        /// Specifies the default Cadence domain for this client.  This defaults to <c>null</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Specifying a default domain can be convienent for many scenarios, especially for those where
        /// the application workflows and activities are restricted to a single domain (which is pretty common).
        /// </para>
        /// <para>
        /// The default domain can be overridden for individual method calls by passing a value as the optional <b>domain</b>
        /// paramater.  You can also leave this setting as <c>null</c> which will require that values be passed to
        /// the <b>domain</b> parameters.
        /// </para>
        /// </remarks>
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
        /// operations to complete.  This defaults to <b>60 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ClientTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clientTimeout", ApplyNamingConventions = false)]
        [DefaultValue(60.0)]
        public double ClientTimeoutSeconds { get; set; } = 60.0;

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
        /// <para>
        /// The Cadence cluster security token.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This is not currently supported by the .NET Cadence client and should be
        /// left alone for now.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SecurityToken", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "securityToken", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecurityToken { get; set; } = null;
        
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
        /// Optionally specifies the interval at which heartbeats are transmitted to
        /// <b>cadence-proxy</b> as a health check.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HeartbeatIntervalSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "heartbeatIntervalSeconds", ApplyNamingConventions = false)]
        [DefaultValue(5.0)]
        public double HeartbeatIntervalSeconds { get; set; } = 5.0;

        /// <summary>
        /// Returns <see cref="HeartbeatIntervalSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan HeartbeatInterval => TimeSpan.FromSeconds(HeartbeatIntervalSeconds);

        /// <summary>
        /// Optionally specifies the maximum time to allow the <b>cadence-proxy</b>
        /// to respond to a heartbeat message.  The proxy will be considered to be 
        /// unhealthy when this happens.  This defaults to <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HeartbeatTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "heartbeatTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(5.0)]
        public double HeartbeatTimeoutSeconds { get; set; } = 5.0;

        /// <summary>
        /// Returns <see cref="HeartbeatTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan HeartbeatTimeout => TimeSpan.FromSeconds(HeartbeatTimeoutSeconds);

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
        /// Specifies the default maximum workflow execution time.  This defaults to <b>24 hours</b>.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowScheduleToCloseTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workflowScheduleToCloseTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(defaultTimeoutSeconds)]
        public double WorkflowScheduleToCloseTimeoutSeconds { get; set; } = defaultTimeoutSeconds;

        /// <summary>
        /// Returns <see cref="WorkflowScheduleToCloseTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan WorkflowScheduleToCloseTimeout => TimeSpan.FromSeconds(Math.Max(WorkflowScheduleToCloseTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the default maximum time a workflow can wait between being scheduled
        /// and actually begin executing.  This defaults to <c>24 hours</c>.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowScheduleToStartTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workflowScheduleToStartTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(defaultTimeoutSeconds)]
        public double WorkflowScheduleToStartTimeoutSeconds { get; set; } = defaultTimeoutSeconds;

        /// <summary>
        /// Returns <see cref="WorkflowScheduleToStartTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan WorkflowScheduleToStartTimeout => TimeSpan.FromSeconds(Math.Max(WorkflowScheduleToStartTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the default maximum time a workflow decision task may execute.
        /// This must be with the range of <b>1 &lt; value &lt;= 60</b> seconds.
        /// This defaults to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowTaskStartToCloseTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workflowTaskStartToCloseTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(10.0)]
        public double WorkflowTaskStartToCloseTimeoutSeconds { get; set; } = 10.0;

        /// <summary>
        /// Returns <see cref="WorkflowTaskStartToCloseTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan WorkflowTaskStartToCloseTimeout => TimeSpan.FromSeconds(Math.Min(Math.Max(WorkflowTaskStartToCloseTimeoutSeconds, 1), 60));

        /// <summary>
        /// Specifies what happens when Cadence workflows attempt to reuse workflow IDs.
        /// This defaults to <see cref="WorkflowIdReusePolicy.AllowDuplicateFailedOnly"/>.
        /// Workflows can customize this via <see cref="WorkflowOptions"/> or <see cref="ChildWorkflowOptions"/>
        /// or by setting this in the <see cref="WorkflowMethodAttribute"/> tagging the 
        /// workflow entry point method
        /// </summary>
        [JsonProperty(PropertyName = "WorkflowIdReusePolicy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "workflowIdReusePolicy", ApplyNamingConventions = false)]
        [DefaultValue(WorkflowIdReusePolicy.AllowDuplicateFailedOnly)]
        public WorkflowIdReusePolicy WorkflowIdReusePolicy { get; set; } = WorkflowIdReusePolicy.AllowDuplicateFailedOnly;

        /// <summary>
        /// Specifies the default maximum time an activity is allowed to wait after being
        /// scheduled until it's actually scheduled to execute on a worker.  This defaults
        /// to <b>24 hours</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ActivityScheduleToCloseTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "activityScheduleToCloseTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(defaultTimeoutSeconds)]
        public double ActivityScheduleToCloseTimeoutSeconds { get; set; } = defaultTimeoutSeconds;

        /// <summary>
        /// Returns <see cref="ActivityScheduleToCloseTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ActivityScheduleToCloseTimeout => TimeSpan.FromSeconds(Math.Max(ActivityScheduleToCloseTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the default maximum time an activity may run after being started.
        /// This defaults to <b>24</b> hours.
        /// </summary>
        [JsonProperty(PropertyName = "ActivityStartToCloseTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "activityStartToCloseTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(defaultTimeoutSeconds)]
        public double ActivityStartToCloseTimeoutSeconds { get; set; } = defaultTimeoutSeconds;

        /// <summary>
        /// Returns <see cref="ActivityStartToCloseTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ActivityStartToCloseTimeout => TimeSpan.FromSeconds(Math.Max(ActivityStartToCloseTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the default maximum time an activity may wait to be started after being scheduled.
        /// This defaults to <b>24</b> hours.
        /// </summary>
        [JsonProperty(PropertyName = "ActivityScheduleToStartTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "activityScheduleToStartTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(defaultTimeoutSeconds)]
        public double ActivityScheduleToStartTimeoutSeconds { get; set; } = defaultTimeoutSeconds;

        /// <summary>
        /// Returns <see cref="ActivityScheduleToStartTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ActivityScheduleToStartTimeout => TimeSpan.FromSeconds(Math.Max(ActivityScheduleToStartTimeoutSeconds, 0));

        /// <summary>
        /// Specifies the default maximum allowed between activity heartbeats.  Activities that
        /// don't submit heartbeats within the time will be considered to be unhealthy and will
        /// be terminated.  This defaults to <b>60 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ActivityHeartbeatTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "activityHeartbeatTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(60.0)]
        public double ActivityHeartbeatTimeoutSeconds { get; set; } = 60.0;

        /// <summary>
        /// Returns <see cref="ActivityHeartbeatTimeoutSeconds"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        internal TimeSpan ActivityHeartbeatTimeout => TimeSpan.FromSeconds(Math.Max(ActivityHeartbeatTimeoutSeconds, 0));

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
        /// This defaults to <see cref="LogLevel.None"/> which will be appropriate for most
        /// proiduction situations.  You may wish to set this to <see cref="LogLevel.Info"/>
        /// or <see cref="LogLevel.Debug"/> while debugging.
        /// </summary>
        [JsonProperty(PropertyName = "LogLevel", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logLevel", ApplyNamingConventions = false)]
        [DefaultValue(LogLevel.None)]
        public LogLevel LogLevel { get; set; } = LogLevel.None;

        /// <summary>
        /// Optionally specifies that messages from the embedded GOLANG Cadence client 
        /// will be included in the log output.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogCadence", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logCadence", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool LogCadence { get; set; } = false;

        /// <summary>
        /// Optionally specifies that messages from the internal <b>cadence-proxy</b>
        /// code that bridges between .NET and the embedded GOLANG Cadence client
        /// will be included in the log output.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogCadenceProxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logCadenceProxy", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool LogCadenceProxy { get; set; } = false;

        /// <summary>
        /// Optionally enable workflow logging while the workflow is being
        /// replayed from history.  This should generally be enabled only
        /// while debugging.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogDuringReplay", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logDuringReplay", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool LogDuringReplay { get; set; } = false;

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
        /// <b>INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-proxy</b> will
        /// already be running for debugging purposes.  When this is <c>true</c>, the 
        /// <b>cadence-client</b> be hardcoded to listen on <b>127.0.0.2:5001</b> and
        /// the <b>cadence-proxy</b> will be assumed to be listening on <b>127.0.0.2:5000</b>.
        /// This defaults to <c>false.</c>
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool DebugPrelaunched { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally disable health heartbeats.  This can be
        /// useful while debugging the client but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        public bool DebugDisableHeartbeats { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally indicates that the <b>cadence-client</b>
        /// will not perform the <see cref="InitializeRequest"/>/<see cref="InitializeReply"/>
        /// and <see cref="TerminateRequest"/>/<see cref="TerminateReply"/> handshakes 
        /// with the <b>cadence-proxy</b> for debugging purposes.  This defaults to
        /// <c>false</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool DebugDisableHandshakes { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally ignore operation timeouts.  This can be
        /// useful while debugging the client but should never be set for production.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool DebugIgnoreTimeouts { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally disables heartbeat handling by the
        /// emulated <b>cadence-proxy</b> for testing purposes.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool DebugIgnoreHeartbeats { get; set; } = false;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Optionally specifies the timeout to use for 
        /// HTTP requests made to the <b>cadence-proxy</b>.  This defaults to
        /// <b>5 seconds</b>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public TimeSpan DebugHttpTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Returns a copy of the current instance.
        /// </summary>
        /// <returns>Thye cloned <see cref="CadenceSettings"/>.</returns>
        public CadenceSettings Clone()
        {
            return new CadenceSettings()
            {
                ActivityHeartbeatTimeoutSeconds        = this.ActivityHeartbeatTimeoutSeconds,
                ActivityScheduleToCloseTimeoutSeconds  = this.ActivityScheduleToCloseTimeoutSeconds,
                ActivityScheduleToStartTimeoutSeconds  = this.ActivityScheduleToStartTimeoutSeconds,
                ActivityStartToCloseTimeoutSeconds     = this.ActivityStartToCloseTimeoutSeconds,
                BinaryFolder                           = this.BinaryFolder,
                ClientIdentity                         = this.ClientIdentity,
                ClientTimeoutSeconds                   = this.ClientTimeoutSeconds,
                ConnectRetries                         = this.ConnectRetries,
                ConnectRetryDelaySeconds               = this.ConnectRetryDelaySeconds,
                CreateDomain                           = this.CreateDomain,
                Debug                                  = this.Debug,
                DebugDisableHandshakes                 = this.DebugDisableHandshakes,
                DebugDisableHeartbeats                 = this.DebugDisableHeartbeats,
                DebugHttpTimeout                       = this.DebugHttpTimeout,
                DebugIgnoreHeartbeats                  = this.DebugIgnoreHeartbeats,
                DebugIgnoreTimeouts                    = this.DebugIgnoreTimeouts,
                DebugPrelaunched                       = this.DebugPrelaunched,
                DefaultDomain                          = this.DefaultDomain,
                HeartbeatIntervalSeconds               = this.HeartbeatIntervalSeconds,
                HeartbeatTimeoutSeconds                = this.HeartbeatTimeoutSeconds,
                ListenPort                             = this.ListenPort,
                LogCadence                             = this.LogCadence,
                LogCadenceProxy                        = this.LogCadenceProxy,
                LogDuringReplay                        = this.LogDuringReplay,
                LogLevel                               = this.LogLevel,
                ProxyTimeoutSeconds                    = this.ProxyTimeoutSeconds,
                SecurityToken                          = this.SecurityToken,
                Servers                                = this.Servers,
                WorkflowIdReusePolicy                  = this.WorkflowIdReusePolicy,
                WorkflowScheduleToCloseTimeoutSeconds  = this.WorkflowScheduleToCloseTimeoutSeconds,
                WorkflowScheduleToStartTimeoutSeconds  = this.WorkflowScheduleToStartTimeoutSeconds,
                WorkflowTaskStartToCloseTimeoutSeconds = this.WorkflowTaskStartToCloseTimeoutSeconds
            };
        }
    }
}
