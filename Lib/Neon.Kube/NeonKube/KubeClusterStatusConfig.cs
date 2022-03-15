//-----------------------------------------------------------------------------
// FILE:	    KubeClusterStatusConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;
using System.ComponentModel;

namespace Neon.Kube
{
    /// <summary>
    /// Used to describe the status of a neonKUBE cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This status is held to the special Kubernetes config map names <see cref="KubeConst.ClusterStatusConfigMapName"/> 
    /// located in the <see cref="KubeNamespaces.NeonStatus"/> namespace.  This will be serialized as JSON to the
    /// config map's <b>status</b> property.
    /// </para>
    /// <para>
    /// The config map is created initially by cluster setup and is then maintained by neon-cluster-operator thereafter.
    /// </para>
    /// </remarks>
    public class KubeClusterStatusConfig
    {
        //---------------------------------------------------------------------
        // Static members

        private const string statusPropertyName = "status";

        /// <summary>
        /// Constructs an instance by parsing a <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <param name="configMap">The source config map.</param>
        /// <returns>The new <see cref="KubeClusterStatusConfig"/>.</returns>
        public static KubeClusterStatusConfig From(V1ConfigMap configMap)
        {
            Covenant.Requires<ArgumentNullException>(configMap != null, nameof(configMap));

            if (!configMap.Data.TryGetValue("status", out var json))
            {
                throw new InvalidDataException($"Expected the [{configMap}] to have a [{statusPropertyName}] property.");
            }

            return NeonHelper.JsonDeserialize<KubeClusterStatusConfig>(json, strict: true);
        }

        /// <summary>
        /// Constructs an instance from a <see cref="KubeClusterHealth"/>.
        /// </summary>
        /// <param name="clusterHealth">The health information.</param>
        /// <returns>The new <see cref="KubeClusterStatusConfig"/>.</returns>
        public static KubeClusterStatusConfig From(KubeClusterHealth clusterHealth)
        {
            Covenant.Requires<ArgumentNullException>(clusterHealth != null, nameof(clusterHealth));

            return new KubeClusterStatusConfig()
            {
                State   = clusterHealth.State,
                Summary = clusterHealth.Summary
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeClusterStatusConfig()
        {
        }

        /// <summary>
        /// The cluster health state.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeClusterState.Unknown)]
        public KubeClusterState State { get; set; }

        /// <summary>
        /// Human readable text summarizing the cluster health state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; }

        /// <summary>
        /// Converts the instance into a <see cref="V1ConfigMap"/>.
        /// </summary>
        /// <returns>The <see cref="V1ConfigMap"/> holding the status.</returns>
        public V1ConfigMap ToConfigMap()
        {
            var configmap = KubeHelper.CreateKubeObject<V1ConfigMap>(KubeConst.ClusterStatusConfigMapName);

            configmap.Data = new Dictionary<string, string>();
            configmap.Data.Add(statusPropertyName, NeonHelper.JsonSerialize(this));

            return configmap;
        }

        /// <summary>
        /// Converts the instance into a <see cref="KubeClusterStatusConfig"/>.
        /// </summary>
        /// <returns>The new <see cref="KubeClusterHealth"/>.</returns>
        public KubeClusterHealth ToKubeClusterHealth()
        {
            return new KubeClusterHealth()
            {
                State   = this.State,
                Summary = this.Summary
            };
        }
    }
}
