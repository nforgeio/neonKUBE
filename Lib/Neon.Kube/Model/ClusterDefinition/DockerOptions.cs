//-----------------------------------------------------------------------------
// FILE:	    DockerOptions.cs
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
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the Docker options for a neonKUBE.
    /// </summary>
    public class DockerOptions
    {
        private const string defaultVersion = "default";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DockerOptions()
        {
        }

        /// <summary>
        /// <para>
        /// The version of Docker to be installed or <b>default</b> to install a reasonable
        /// version for the version of Kubernetes being deployed.  This defaults to <b>default</b>
        /// which will install a reasonable supported version.
        /// </para>
        /// <note>
        /// Only Community Editions of Docker are supported at this time.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            Version = Version ?? defaultVersion;
            Version = Version.ToLowerInvariant();

            if (!Version.EndsWith("-ce") && Version != defaultVersion)
            {
                throw new ClusterDefinitionException($"[{nameof(DockerOptions)}.{Version}] does not specify a Docker community edition.  neonKUBE only supports Docker Community Edition at this time.");
            }
        }

        /// <summary>
        /// Clears any sensitive properties like the Docker registry credentials.
        /// </summary>
        public void ClearSecrets()
        {
        }
    }
}
