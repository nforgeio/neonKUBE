//-----------------------------------------------------------------------------
// FILE:	    MetricsOptions.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated Prometheus 
    /// metrics stack: <a href="https://prometheus.io/">https://prometheus.io/</a>
    /// </summary>
    public class MetricsOptions
    {
        /// <summary>
        /// Indicates where Prometheus metrics should be stored.  This defaults to <see cref="MetricsStorageOptions.Ephemeral"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Storage", Required = Required.Default)]
        [YamlMember(Alias = "storage", ApplyNamingConventions = false)]
        [DefaultValue(MetricsStorageOptions.Ephemeral)]
        public MetricsStorageOptions Storage { get; set; } = MetricsStorageOptions.Ephemeral;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Nodes.Any(n => n.Labels.Metrics))
            {
                if (clusterDefinition.Kubernetes.AllowPodsOnControlPlane.GetValueOrDefault() == true)
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.MetricsInternal = true;
                    }
                }
                else
                {
                    foreach (var node in clusterDefinition.Workers)
                    {
                        node.Labels.MetricsInternal = true;
                    }
                }
            }
            else
            {
                foreach (var node in clusterDefinition.Nodes.Where(node => node.Labels.Metrics))
                {
                    node.Labels.MetricsInternal = true;
                }
            }
        }
    }
}
