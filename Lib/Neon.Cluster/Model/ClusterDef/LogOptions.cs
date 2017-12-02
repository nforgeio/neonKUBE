//-----------------------------------------------------------------------------
// FILE:	    LogOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the logging options for a neonCLUSTER.
    /// </summary>
    public class LogOptions
    {
        private const bool      defaultEnabled            = true;
        private const string    defaultHostImage          = "neoncluster/neon-log-host:latest";
        private const string    defaultCollectorImage     = "neoncluster/neon-log-collector:latest";
        private const string    defaultEsImage            = "neoncluster/elasticsearch:latest";
        private const int       defaultEsShards           = 8;
        private const int       defaultEsReplication      = 1;
        private const string    defaultEsMemory           = "2GB";
        private const string    defaultKibanaImage        = "neoncluster/kibana:latest";
        private const string    defaultMetricbeatImage    = "neoncluster/metricbeat:latest";
        
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LogOptions()
        {
        }

        /// <summary>
        /// Indicates whether the logging pipeline is to be enabled on the cluster.
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEnabled)]
        public bool Enabled { get; set; } = defaultEnabled;

        /// <summary>
        /// Identifies the <b>Elasticsearch</b> container image to be deployed on the cluster to persist
        /// cluster log events.  This defaults to <b>neoncluster/elasticsearch:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "EsImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEsImage)]
        public string EsImage { get; set; } = defaultEsImage;

        /// <summary>
        /// The number of Elasticsearch shards to be configured.  This essentially specifies
        /// the ultimate scalablity of the logging cluster.  This defaults to <b>8</b>.
        /// </summary>
        [JsonProperty(PropertyName = "EsShards", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEsShards)]
        public int EsShards { get; set; } = defaultEsShards;

        /// <summary>
        /// The number of times Elasticsearch will replicate data within the
        /// logging cluster for fault tolerance.  This defaults to <b>1</b>
        /// which ensures that the greatest data capacity at the cost of
        /// no fault tolerance.
        /// </summary>
        [JsonProperty(PropertyName = "EsReplicas", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEsReplication)]
        public int EsReplicas { get; set; } = defaultEsReplication;

        /// <summary>
        /// The amount of RAM to dedicate to each cluster log related Elasticsearch container.
        /// This can be expressed as the number of bytes or a number with one of these unit
        /// suffixes: <b>B, K, KB, M, MB, G, or GB</b>.  This defaults to <b>2GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "EsMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultEsMemory)]
        public string EsMemory { get; set; } = defaultEsMemory;

        /// <summary>
        /// Returns the number of bytes of RAM to to dedicate to a log related Elasticsearch
        /// container by parsing <see cref="EsMemory"/>.
        /// </summary>
        [JsonIgnore]
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
        /// Identifies the <b>Kibana</b> container image to be deployed on the cluster to present
        /// the cluster logging user interface.  This defaults to <b>neoncluster/kibana:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KibanaImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultKibanaImage)]
        public string KibanaImage { get; set; } = defaultKibanaImage;

        /// <summary>
        /// Identifies the <b>td-agent</b> service image to be run locally on every manager and worker node.  This container
        /// acts as the entrypoint to the cluster's log aggregation pipeline.  This defaults to <b>neoncluster/neon-log-host:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HostImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultHostImage)]
        public string HostImage { get; set; } = defaultHostImage;

        /// <summary>
        /// Identifies the <b>td-agent</b> container image to be run on the cluster, acting as the downstream event 
        /// aggregator for all of the cluster nodes.  This defaults to <b>neoncluster/neon-log-collector:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CollectorImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultCollectorImage)]
        public string CollectorImage { get; set; } = defaultCollectorImage;

        /// <summary>
        /// Identifies the <b>Elastic Metricbeat</b> container image to be run on each node of the cluster to capture
        /// Docker host node metrics.  This defaults to <b>neoncluster/metricbeat:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MetricbeatImage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMetricbeatImage)]
        public string MetricbeatImage { get; set; } = defaultMetricbeatImage;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (!Enabled)
            {
                return;
            }

            if (clusterDefinition.Nodes.Count(n => n.Labels.LogEsData) == 0)
            {
                throw new ClusterDefinitionException($"Invalid Log Configuration: At least one node must be labeled with [{NodeLabels.LabelLogEsData}=true].");
            }

            if (string.IsNullOrWhiteSpace(EsImage))
            {
                throw new ClusterDefinitionException($"Missing [{nameof(LogOptions)}.{nameof(EsImage)} setting.");
            }

            if (EsShards <= 0)
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(EsShards)}={EsShards}]: This must be >= 1.");
            }

            if (EsReplicas <= 0)
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(EsReplicas)}={EsReplicas}]: This must be >= 1.");
            }

            if (string.IsNullOrWhiteSpace(KibanaImage))
            {
                throw new ClusterDefinitionException($"Missing [{nameof(LogOptions)}.{nameof(KibanaImage)} setting.");
            }

            if (string.IsNullOrWhiteSpace(HostImage))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(HostImage)}={HostImage}].");
            }

            if (string.IsNullOrWhiteSpace(CollectorImage))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(LogOptions)}.{nameof(CollectorImage)}={CollectorImage}].");
            }
        }
    }
}
