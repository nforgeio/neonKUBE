//-----------------------------------------------------------------------------
// FILE:	    Registry.cs
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
    /// <para>
    /// Specifies details about an upstream container registry.  This can be used to block, remap or indicate
    /// that insecure HTTP requests can be used for specific registries.  This will be used to generate the
    /// <b>[[registry]]</b> entries in the <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file
    /// on the cluster nodes.
    /// </para>
    /// <para>
    /// See more details here: <a href="https://github.com/containers/image/blob/main/docs/containers-registries.conf.5.md">here</a>
    /// </para>
    /// </summary>
    public class Registry
    {
        /// <summary>
        /// <para>
        /// Specifies the name to be used when persisting this as a <b>V1ContainerRegistry</b> to the cluster.
        /// This must be a valid Kubernetes name:
        /// </para>
        /// <list type="bullet">
        /// <item>contain no more than 253 characters</item>
        /// <item>contain only lowercase alphanumeric characters, '-' or '.'</item>
        /// <item>start with an alphanumeric character</item>
        /// <item>end with an alphanumeric character</item>
        /// </list>
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies registry prefix, optionally with a subdomain <b>"*"</b> wildcard character for subdomain matching.
        /// </summary>
        [JsonProperty(PropertyName = "Prefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "prefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Prefix { get; set; } = null;

        /// <summary>
        /// Optionally indicates that insecure HTTP requests may be used to access the registry.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Insecure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecure", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Insecure { get; set; } = false;

        /// <summary>
        /// Optionally blocks pulls of images from registries that match <see cref="Prefix"/>.  
        /// This defaults to <c>false.</c>
        /// </summary>
        [JsonProperty(PropertyName = "Blocked", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "blocked", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Blocked { get; set; } = false;

        /// <summary>
        /// Optionally enables registry redirection.  When specified, this indicates that images 
        /// specified to be pulled from <see cref="Prefix"/> will actually be pulled from 
        /// <see cref="Location"/> instead.  This is a nice way to be able to reuse manifests
        /// and Helm charts such that they pull images from an alternate registry without
        /// modification.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Location", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "location", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Location { get; set; } = null;

        /// <summary>
        /// Optionally specifies the username to be used for authenticating with the upstream
        /// container registry.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Username { get; set; } = null;

        /// <summary>
        /// Optionally specifies the password to be used for authenticating with the upstream
        /// container registry.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Password { get; set; } = null;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            ContainerOptions.ValidateRegistryPrefix(Prefix, allowWildcard: true, propertyPath: $"{nameof(ClusterDefinition.Container)}.{nameof(ContainerOptions.Registries)}.{nameof(Prefix)}");

            try
            {
                KubeHelper.CheckName(this.Name);
            }
            catch (Exception e)
            {
                throw new ClusterDefinitionException(e.Message, e);
            }

            if (!Prefix.StartsWith("*.") && !string.IsNullOrEmpty(Location))
            {
                throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Container)}.{nameof(Prefix)}={Prefix}]: [{nameof(Location)}] required when the prefix doesn't include a wildcard like: *.example.com ");
            }

            if (!string.IsNullOrEmpty(Location))
            {
                ContainerOptions.ValidateRegistryPrefix(Location, allowWildcard: true, propertyPath: $"{nameof(ClusterDefinition.Container)}.{nameof(ContainerOptions.Registries)}.{nameof(Location)}");
            }
        }
    }
}
