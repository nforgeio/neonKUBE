//-----------------------------------------------------------------------------
// FILE:	    ClusterInfo.cs
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

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// <para>
    /// Summarizes the state of a cluster.
    /// </para>
    /// <note>
    /// This does not include any sensitive information so it should be
    /// reasonable to be able to upload this automatically as telemetry
    /// and also to include in GitHub issues.
    /// </note>
    /// </summary>
    public class ClusterSummary
    {
        //---------------------------------------------------------------------
        // Local types

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the summary from a cluster.
        /// </summary>
        /// <param name="cluster">The target cluster proxy.</param>
        /// <param name="definition">Optionally overrides the cluster definition passed within <paramref name="cluster"/>.</param>
        /// <returns>The <see cref="ClusterSummary"/>.</returns>
        public static ClusterSummary FromCluster(ClusterProxy cluster, ClusterDefinition definition = null)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            if (definition == null)
            {
                definition = cluster.Definition;
            }

            var summary = new ClusterSummary();

            // Load the cluster globals.

            var globals   = cluster.Consul.KV.DictionaryOrEmpty(NeonClusterConst.ClusterGlobalsKey).Result;
            var internals = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                // We don't include these internal globals in the summary.

                NeonClusterGlobals.DefinitionDeflate,
                NeonClusterGlobals.DefinitionHash,
                NeonClusterGlobals.PetsDefinition
            };

            foreach (var item in globals.Where(i => !internals.Contains(i.Key)))
            {
                summary.Globals.Add(item.Key, Encoding.UTF8.GetString(item.Value));
            }

            // Summarize information from the cluster definition.

            summary.NodeCount          = definition.Nodes.Count();
            summary.ManagerCount       = definition.Managers.Count();
            summary.WorkerCount        = definition.Workers.Count();
            summary.PetCount           = definition.Pets.Count();
            summary.OperatingSystem    = definition.HostNode.OperatingSystem;
            summary.HostingEnvironment = definition.Hosting.Environment;
            summary.CephEnabled        = definition.Ceph.Enabled;
            summary.LogEnabled         = definition.Log.Enabled;
            summary.VpnEnabled         = definition.Vpn.Enabled;

            return summary;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterSummary()
        {
        }

        /// <summary>
        /// A dictionary including all of the cluster's global Consul settings and values.
        /// </summary>
        [JsonProperty(PropertyName = "Globals", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public Dictionary<string, string> Globals { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns the cluster's creation date (UTC).
        /// </summary>
        [JsonIgnore]
        public DateTime CreateDateUtc
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.CreateDateUtc, out string value))
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
        /// Returns the version of <b>neon-cli</b> that created or last upgraded the cluster.
        /// </summary>
        [JsonIgnore]
        public string NeonCliVersion
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.NeonCliVersion, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the minimum version of <b>neon-cli</b> allowed to manager the cluster.
        /// </summary>
        [JsonIgnore]
        public string NeonCliVersionMinimum
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.NeonCliVersionMinimum, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the cluster's unique ID.
        /// </summary>
        [JsonIgnore]
        public string Uuid
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.Uuid, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the number of days that cluster logs will be retained.
        /// </summary>
        [JsonProperty(PropertyName = "LogRetentionDays", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [JsonIgnore]
        public int LogRetentionDays
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.LogRetentionDays, out string value) ||
                    !int.TryParse(value, out var logRetentionDays))
                {
                    return -1;
                }

                return logRetentionDays;
            }
        }

        /// <summary>
        /// Returns the total number of cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "NodeCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int NodeCount { get; set; }

        /// <summary>
        /// Returns the number of cluster manager nodes.
        /// </summary>
        [JsonProperty(PropertyName = "ManagerCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int ManagerCount { get; set; }

        /// <summary>
        /// Returns the number of cluster worker nodes.
        /// </summary>
        [JsonProperty(PropertyName = "WorkerCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int WorkerCount { get; set; }

        /// <summary>
        /// Returns the number of cluster pet nodes.
        /// </summary>
        [JsonProperty(PropertyName = "PetCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(-1)]
        public int PetCount { get; set; }

        /// <summary>
        /// Identifies the operating system deployed on the cluster host nodes.
        /// </summary>
        [JsonProperty(PropertyName = "OperatingSystem", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(TargetOS.Unknown)]
        public TargetOS OperatingSystem { get; set; }

        /// <summary>
        /// Identifies the cluster hosting environment.
        /// </summary>
        [JsonProperty(PropertyName = "HostingEnvironment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(HostingEnvironments.Unknown)]
        public HostingEnvironments HostingEnvironment { get; set; }

        /// <summary>
        /// Indicates that Ceph is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "CephEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool CephEnabled { get; set; }

        /// <summary>
        /// Indicates that cluster logging is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "LogEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool LogEnabled { get; set; }

        /// <summary>
        /// Indicates that cluster VPN is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "VpnEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool VpnEnabled { get; set; }
    }
}
