//-----------------------------------------------------------------------------
// FILE:        ClusterResetOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;
using Neon.Tasks;

namespace Neon.Kube.Proxy
{
    /// <summary>
    /// Specifies options for resetting an existing cluster.
    /// </summary>
    public class ClusterResetOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterResetOptions()
        {
        }

        /// <summary>
        /// <para>
        /// Enable resetting the Harbor to its original configuration.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "ResetHarbor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool ResetHarbor { get; set; } = true;

        /// <summary>
        /// <para>
        /// Resets <b>Minio</b> by removing any custom buckets.  Note that existing
        /// buckets holding Harbor, Loki, or Mirmir information will remain unchanged when
        /// this is enabled.  The <see cref="ResetHarbor"/>, <see cref="ResetMonitoring"/>, 
        /// options control clearing of the related Minio data when enabled.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "ResetMinio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool ResetMinio { get; set; } = true;

        /// <summary>
        /// <para>
        /// Resets the CRI-O runtime on each cluster node by removing any non-factory deployed
        /// container images.  This also resets the container registry custom resources to match 
        /// the original cluster definition.  These configure how the <b>CRI-O</b> container runtime 
        /// on all cluster nodes reference external container registries such as DockerHub, GitHub, 
        /// Quay, etc. as well as private registries.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "ResetCrio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool ResetCrio { get; set; } = true;

        /// <summary>
        /// <para>
        /// Resets <b>Dex/Glauth</b> by removing any non-factory deployed users and other configuration.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "ResetAuth", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool ResetAuth { get; set; } = true;

        /// <summary>
        /// <para>
        /// Resets monitoring by clearing any recorded logs and metrics and restoring any Grafana
        /// dashboards and alerts to the factory defaults.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "ResetMonitoring", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool ResetMonitoring { get; set; } = true;

        /// <summary>
        /// <para>
        /// Specifies namespaces to be excluded from those being removed during the reset operation.
        /// Normally, all namespaces beside the internal NEONKUBE namespaces will be removed with
        /// the <b>default</b> being recreated as empty thereafter.
        /// </para>
        /// <note>
        /// Pass a namespace as <b>"*"</b> to retain all non-standard namespaces.
        /// </note>
        /// <para>
        /// This defaults to an empty list.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "KeepNamespaces", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> KeepNamespaces { get; set; } = new List<string>();

        /// <summary>
        /// <para>
        /// Specifies the number of seconds to wait for the cluster to stabilize after performing
        /// the reset.  This defaults to <b>30 seconds</b>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "StabilizeSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(30)]
        public int StabilizeSeconds { get; set; } = 30;
    }
}
