//-----------------------------------------------------------------------------
// FILE:	    ClusterStatus.cs
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

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds details about a cluster's state.
    /// </summary>
    public class ClusterStatus
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterStatus()
        {
        }

        /// <summary>
        /// The neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string Version { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// Indicates whether the cluster is currently locked, unlocked, or whether the
        /// lock state is currentlt unknown (when <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "IsLocked", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? IsLocked { get; set; } = null;

        /// <summary>
        /// Describes the overall state of a cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Status", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ClusterState.Unknown)]
        public ClusterState State { get; set; } = ClusterState.Unknown;

        /// <summary>
        /// Maps node names to their provisioning states.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, ClusterNodeState> Nodes { get; set; } = new Dictionary<string, ClusterNodeState>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Human readable string that summarizes the cluster state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; } = null;

        /// <summary>
        /// Describes which optional components have been deployed to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "OptionalComponents", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ClusterOptionalComponents OptionalComponents { get; set; } = new ClusterOptionalComponents();
    }
}
