//-----------------------------------------------------------------------------
// FILE:	    HiveSummary.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// <para>
    /// Summarizes the state of a hive.
    /// </para>
    /// <note>
    /// This does not include any sensitive information so it should be
    /// reasonable to be able to upload this automatically as telemetry
    /// and also to include in GitHub issues.
    /// </note>
    /// </summary>
    public class HiveSummary
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the summary from a hive proxy.
        /// </summary>
        /// <param name="hive">The target hive proxy.</param>
        /// <param name="definition">Optionally overrides the hive definition passed within <paramref name="hive"/>.</param>
        /// <returns>The <see cref="HiveSummary"/>.</returns>
        public static HiveSummary FromHive(HiveProxy hive, HiveDefinition definition = null)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            var summary = new HiveSummary();

            // Load the hive globals.

            var globals   = hive.Consul.Client.KV.DictionaryOrEmpty(HiveConst.GlobalKey).Result;
            var internals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                // We don't include these internal globals in the summary.

                HiveGlobals.DefinitionDeflate,
                HiveGlobals.DefinitionHash,
                HiveGlobals.PetsDefinition
            };

            foreach (var item in globals.Where(i => !internals.Contains(i.Key)))
            {
                summary.Globals.Add(item.Key, Encoding.UTF8.GetString(item.Value));
            }

            // Summarize information from the hive definition.

            summary.NodeCount          = definition.Nodes.Count();
            summary.ManagerCount       = definition.Managers.Count();
            summary.WorkerCount        = definition.Workers.Count();
            summary.PetCount           = definition.Pets.Count();
            summary.OperatingSystem    = definition.HiveNode.OperatingSystem;
            summary.HostingEnvironment = definition.Hosting.Environment;
            summary.HiveFSEnabled      = definition.HiveFS.Enabled;
            summary.LogEnabled         = definition.Log.Enabled;
            summary.VpnEnabled         = definition.Vpn.Enabled;

            return summary;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HiveSummary()
        {
        }

        /// <summary>
        /// A dictionary including all of the hive's global Consul settings and values.
        /// </summary>
        [JsonProperty(PropertyName = "Globals", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public Dictionary<string, string> Globals { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns the hive's creation date (UTC).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public DateTime CreateDateUtc
        {
            get
            {
                if (!Globals.TryGetValue(HiveGlobals.CreateDateUtc, out string value))
                {
                    value = null;
                }

                if (!DateTime.TryParse("", out var date))
                {
                    date = default(DateTime);
                }

                return date;
            }
        }

        /// <summary>
        /// Returns the hive version, actually the version of <b>neon-cli</b> that 
        /// created or last upgraded the hive.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Version
        {
            get
            {
                if (!Globals.TryGetValue(HiveGlobals.Version, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the minimum version of <b>neon-cli</b> allowed to manager the hive.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string NeonCliVersion
        {
            get
            {
                if (!Globals.TryGetValue(HiveGlobals.NeonCli, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the hive's unique ID.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Uuid
        {
            get
            {
                if (!Globals.TryGetValue(HiveGlobals.Uuid, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the number of days that hive logs will be retained.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int LogRetentionDays
        {
            get
            {
                if (!Globals.TryGetValue(HiveGlobals.UserLogRetentionDays, out string value) ||
                    !int.TryParse(value, out var logRetentionDays))
                {
                    return -1;
                }

                return logRetentionDays;
            }
        }

        /// <summary>
        /// Returns the total number of hive nodes.
        /// </summary>
        [JsonProperty(PropertyName = "NodeCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int NodeCount { get; set; }

        /// <summary>
        /// Returns the number of hive manager nodes.
        /// </summary>
        [JsonProperty(PropertyName = "ManagerCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int ManagerCount { get; set; }

        /// <summary>
        /// Returns the number of hive worker nodes.
        /// </summary>
        [JsonProperty(PropertyName = "WorkerCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int WorkerCount { get; set; }

        /// <summary>
        /// Returns the number of hive pet nodes.
        /// </summary>
        [JsonProperty(PropertyName = "PetCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int PetCount { get; set; }

        /// <summary>
        /// Identifies the operating system deployed on the hive host nodes.
        /// </summary>
        [JsonProperty(PropertyName = "OperatingSystem", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(TargetOS.Unknown)]
        public TargetOS OperatingSystem { get; set; }

        /// <summary>
        /// Identifies the hive hosting environment.
        /// </summary>
        [JsonProperty(PropertyName = "HostingEnvironment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(HostingEnvironments.Unknown)]
        public HostingEnvironments HostingEnvironment { get; set; }

        /// <summary>
        /// Indicates that Ceph is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "HiveFSEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool HiveFSEnabled { get; set; }

        /// <summary>
        /// Indicates that hive logging is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "LogEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool LogEnabled { get; set; }

        /// <summary>
        /// Indicates that hive VPN is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "VpnEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool VpnEnabled { get; set; }
    }
}
