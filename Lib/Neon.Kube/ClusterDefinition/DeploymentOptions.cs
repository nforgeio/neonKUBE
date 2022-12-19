//-----------------------------------------------------------------------------
// FILE:	    DeploymentOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies cluster deployment options used by <b>ClusterFixture</b> as well
    /// as potentially by custom tools.
    /// </summary>
    public class DeploymentOptions
    {
        /// <summary>
        /// <para>
        /// Optional prefix combined with the cluster name to generate the resource group name
        /// when deploying the cluster to a cloud environment or combined with the node name
        /// for other environments.  This is typically used by unit tests deployed by <b>ClusterFixture</b>
        /// but can also be used by custom tools to avoid conflicts when multiple tests may be 
        /// running in parallel (probably on different machines) as well as providing a way to 
        /// identify and remove clusters or VMs orphaned by previous interrupted tests or tool runs.
        /// </para>
        /// <para>
        /// This will typically be set to something identifying the machine, user, and/or tool
        /// running the test like <b>runner0</b>, <b>jeff</b>, or <b>runner0-jeff</b>.
        /// </para>
        /// <note>
        /// A dash will be appended automatically to non-<c>null</c> prefixes before prepending this
        /// to the cluster name.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Prefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "prefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Prefix { get; set; } = null;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (!string.IsNullOrEmpty(Prefix) && !ClusterDefinition.DnsNameRegex.IsMatch(Prefix))
            {
                throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Deployment)}.{nameof(Prefix)}={Prefix}] is not a valid prefix.");
            }
        }

        /// <summary>
        /// Prefixes the name passed with <see cref="Prefix"/> and a dash when 
        /// <see cref="Prefix"/> is not <c>null</c> or empty.
        /// </summary>
        /// <param name="name">The name being prefixed.</param>
        /// <returns>The prefixed name.</returns>
        public string GetPrefixedName(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (!string.IsNullOrEmpty(Prefix))
            {
                name = $"{Prefix}-{name}";
            }

            return name.ToLowerInvariant();
        }
    }
}
