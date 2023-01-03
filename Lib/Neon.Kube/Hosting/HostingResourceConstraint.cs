//-----------------------------------------------------------------------------
// FILE:	    HostingResourceConstraint.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// Describes a resource constraint that will prevent a cluster from being deployed.
    /// </summary>
    public class HostingResourceConstraint
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public HostingResourceConstraint()
        {
        }

        /// <summary>
        /// Indicates the constrained resource type: Memory, CPU, disk,...
        /// </summary>
        [JsonProperty(PropertyName = "ResourceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "resourceType", ApplyNamingConventions = false)]
        [DefaultValue(HostingConstrainedResourceType.Unknown)]
        public HostingConstrainedResourceType ResourceType { get; set; }

        /// <summary>
        /// Lists the cluster nodes by name that are impacted by the resource constraint.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Nodes { get; set; } = new List<string>();

        /// <summary>
        /// Returns an optional string with additional human readable details about
        /// the hosting environment's resource constraint.
        /// </summary>
        [JsonProperty(PropertyName = "Details", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "details", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Details { get; set; }
    }
}
