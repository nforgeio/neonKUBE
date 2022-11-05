//-----------------------------------------------------------------------------
// FILE:	    ContainerOptions.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Specifies CRI-O and local container registry options.
    /// </para>
    /// <note>
    /// These options can be used to customize the <b>/etc/containers/registries.conf</b> on the cluster
    /// nodes as the cluster is provisioned.  See more information: <a href="https://github.com/containers/image/blob/main/docs/containers-registries.conf.5.md">here</a>
    /// </note>
    /// </summary>
    public class ContainerOptions
    {
        //---------------------------------------------------------------------
        // Static members

        private const string defaultRegistry = "docker.io";

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Ensures that a registry prefix is valid.
        /// </summary>
        /// <param name="registryPrefix">The registry prefix being checked.</param>
        /// <param name="allowWildcard">Indicates whether a wildcard prefix is allowed.</param>
        /// <param name="propertyPath">Identifies the specific cluster definition property being checked.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        /// <remarks>
        /// <para>
        /// Supported prefixes may look like one of
        /// </para>
        /// <code>
        /// host[:port]
        /// host[:port]/namespace[/namespace…]
        /// host[:port]/namespace[/namespace…]/repo
        /// [*.]host
        /// </code>
        /// </remarks>
        internal static void ValidateRegistryPrefix(string registryPrefix, bool allowWildcard, string propertyPath)
        {
            var error = $"Registry [{propertyPath}={registryPrefix}] is not valid.";

            if (string.IsNullOrEmpty(registryPrefix))
            {
                throw new ClusterDefinitionException(error);
            }

            // Strip off any leading [.*] wildcard when these are allowed.

            var prefix = registryPrefix;

            if (allowWildcard && prefix.StartsWith("*."))
            {
                prefix = prefix.Substring(2);
            }

            // Parse the prefix as a URI to make it easier to check.

            if (!Uri.TryCreate($"http://{prefix}", UriKind.Absolute, out var uri))
            {
                throw new ClusterDefinitionException(error);
            }

            // Host names are only allowed to have alphanumeric, digits, dashes or periods.

            foreach (var ch in uri.Host)
            {
                if (!char.IsLetterOrDigit(ch) && ch != '.' && ch != '-' && ch != '_')
                {
                    throw new ClusterDefinitionException(error);
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the prefixes for the default container registeries to be searched when pulling
        /// container images that don't identify a source registry.  This defaults to Docker Hub (<b>docker.io</b>)
        /// but zero or more custom DNS hostnames or IP addresses may be specified.
        /// </para>
        /// <note>
        /// Container registries will be searched for containers in the order that registries appear in
        /// this list.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SearchRegistries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "searchRegistries", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> SearchRegistries { get; set; }

        /// <summary>
        /// <para>
        /// Optionally customizes how pulls from one or more upstream container registries are performed.
        /// This can be used to block registry access, allow insecure HTTP access, or remap target
        /// registeries to another location.
        /// </para>
        /// <note>
        /// These items will generate corresponding <b>[[registry]]</b> items in the 
        /// <b>/etc/containers/registries.conf</b> file on the cluster nodes.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Registries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "registries", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Registry> Registries { get; set; } = new List<Registry>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ContainerOptions()
        {
            SearchRegistries = new List<string>() { defaultRegistry };
        }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            SearchRegistries = SearchRegistries ?? new List<string>() { "docker.io" };
            Registries       = Registries ?? new List<Registry>();

            // Ensure that [SearchRegistries] references are formatted correctly.

            SearchRegistries = SearchRegistries ?? new List<string>() { defaultRegistry };

            foreach (var registry in SearchRegistries)
            {
                if (string.IsNullOrEmpty(registry))
                {
                    continue;
                }

                ValidateRegistryPrefix(registry, allowWildcard: false, propertyPath: $"{nameof(ClusterDefinition.Container)}.{nameof(SearchRegistries)}");
            }

            // Ensure that any registry customizations are valid.

            foreach (var registry in Registries)
            {
                if (registry == null)
                {
                    continue;
                }

                ValidateRegistryPrefix(registry.Prefix, allowWildcard: true, propertyPath: $"{nameof(ClusterDefinition.Container)}.{nameof(Registries)}.{nameof(registry.Prefix)}");

                if (!string.IsNullOrEmpty(registry.Location))
                {
                    ValidateRegistryPrefix(registry.Location, allowWildcard: true, propertyPath: $"{nameof(ClusterDefinition.Container)}.{nameof(Registries)}.{nameof(registry.Location)}");
                }
            }
        }
    }
}
