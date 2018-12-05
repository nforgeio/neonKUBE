//-----------------------------------------------------------------------------
// FILE:	    LogOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the logging options for a neonHIVE.
    /// </summary>
    public class LogOptions
    {
        private const bool      defaultEnabled         = true;
        private const string    defaultEsMemory        = "1.5GB";
        private const int       defaultRetentionDays   = 14;
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LogOptions()
        {
        }

        /// <summary>
        /// Indicates whether the logging pipeline is to be enabled on the hive.
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEnabled)]
        public bool Enabled { get; set; } = defaultEnabled;

        /// <summary>
        /// The amount of RAM to dedicate to each hive log related Elasticsearch container.
        /// This can be expressed as the number of bytes or a number with one of these unit
        /// suffixes: <b>B, K, KB, M, MB, G, or GB</b>.  This defaults to <b>1.5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "EsMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEsMemory)]
        public string EsMemory { get; set; } = defaultEsMemory;

        /// <summary>
        /// The positive number of days of logs to be retained in the hive Elasticsearch hive.
        /// This defaults to <b>14 days</b>.
        /// </summary>
        [JsonProperty(PropertyName = "RetentionDays", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRetentionDays)]
        public int RetentionDays { get; set; } = defaultRetentionDays;

        /// <summary>
        /// Returns the number of bytes of RAM to dedicate to a log related Elasticsearch
        /// container by parsing <see cref="EsMemory"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public long EsMemoryBytes
        {
            get
            {
                double byteCount;

                if (!NeonHelper.TryParseCount(EsMemory, out byteCount))
                {
                    throw new FormatException($"Invalid [{nameof(LogOptions)}.{nameof(EsMemory)}={EsMemory}].");
                }

                return (long)byteCount;
            }
        }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (!Enabled)
            {
                return;
            }

            var esNodeCount = hiveDefinition.Nodes.Count(n => n.Labels.LogEsData);

            if (esNodeCount == 0)
            {
                throw new HiveDefinitionException($"Invalid Log Configuration: At least one node must be labeled with [{NodeLabels.LabelLogEsData}=true].");
            }

            if (RetentionDays <= 0)
            {
                throw new HiveDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(RetentionDays)}={RetentionDays}]: This must be >= 0.");
            }

            if (!NeonHelper.TryParseCount(EsMemory, out var esMemoryBytes))
            {
                throw new HiveDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(EsMemory)}={EsMemory}].");
            }

            if (esMemoryBytes < 1.5 * NeonHelper.Giga)
            {
                throw new HiveDefinitionException($"[{nameof(LogOptions)}.{nameof(EsMemory)}={EsMemory}] cannot be less than [1.5GB].");
            }
        }
    }
}
