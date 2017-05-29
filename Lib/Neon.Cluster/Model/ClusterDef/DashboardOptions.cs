//-----------------------------------------------------------------------------
// FILE:	    DashboardOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the dashboard to be installed in cluster.
    /// </summary>
    public class DashboardOptions
    {
        private const bool defaultKibana = true;
        private const bool defaultConsul = true;

        /// <summary>
        /// Installs the Elastic Kibana dashboard if logging is enabled for the cluster.
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Kibana", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultKibana)]
        public bool Kibana { get; set; } = defaultKibana;

        /// <summary>
        /// Installs the Consul user interface.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Consul", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultConsul)]
        public bool Consul { get; set; } = defaultConsul;

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
        }
    }
}
