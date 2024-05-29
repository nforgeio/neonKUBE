//-----------------------------------------------------------------------------
// FILE:        GoogleHostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the Google Cloud Platform hosting settings.
    /// </summary>
    public class GoogleHostingOptions
    {
        // $todo(jefflill): These will need refactoring once we actually support Google Cloud.

        private const string defaultVnetSubnet = "10.100.0.0/24";
        private const string defaultNodeSubnet = "10.100.0.0/24";

        /// <summary>
        /// Constructor.
        /// </summary>
        public GoogleHostingOptions()
        {
        }

        /// <summary>
        /// Specifies the subnet for the Azure VNET.  This defaults to <b>10.100.0.0/24</b>
        /// </summary>
        [JsonProperty(PropertyName = "VnetSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vnetSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultVnetSubnet)]
        public string VnetSubnet { get; set; } = defaultVnetSubnet;

        /// <summary>
        /// specifies the subnet within <see cref="VnetSubnet"/> where the cluster nodes will be provisioned.
        /// This defaults to <b>10.100.0.0/24</b>.
        /// </summary>
        [JsonProperty(PropertyName = "NodeSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodeSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultNodeSubnet)]
        public string NodeSubnet { get; set; } = defaultNodeSubnet;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Hosting)}.{nameof(ClusterDefinition.Hosting.Google)}";

            // Verify subnets

            if (!NetworkCidr.TryParse(VnetSubnet, out var vnetSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(VnetSubnet)}={VnetSubnet}] is not a valid subnet.");
            }

            if (!NetworkCidr.TryParse(NodeSubnet, out var nodeSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(NodeSubnet)}={NodeSubnet}] is not a valid subnet.");
            }

            if (!vnetSubnet.Contains(nodeSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(NodeSubnet)}={NodeSubnet}] is contained within [{nameof(VnetSubnet)}={VnetSubnet}].");
            }
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            throw new NotImplementedException();
        }
    }
}
