//-----------------------------------------------------------------------------
// FILE:	    ClusterOptionalComponents.cs
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
using System.Runtime.Serialization;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// Indicates which optional factory components have been deployed to the cluster.
    /// </summary>
    public class ClusterOptionalComponents
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterOptionalComponents()
        {
        }

        /// <summary>
        /// Indicates whether <b>Harbor</b> is installed.
        /// </summary>
        [JsonProperty(PropertyName = "Harbor", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Harbor { get; set; } = false;

        /// <summary>
        /// Indicates whether <b>Minio</b> is installed.
        /// </summary>
        [JsonProperty(PropertyName = "Minio", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Minio { get; set; } = false;

        /// <summary>
        /// Indicates whether <b>Grafana</b> is installed.
        /// </summary>
        [JsonProperty(PropertyName = "Grafana", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Grafana { get; set; } = false;

        /// <summary>
        /// Indicates whether <b>Loki</b> is installed.
        /// </summary>
        [JsonProperty(PropertyName = "Loki", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Loki { get; set; } = false;

        /// <summary>
        /// Indicates whether <b>Cortex</b> is installed.
        /// </summary>
        [JsonProperty(PropertyName = "Cortex", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Cortex { get; set; } = false;
    }
}
