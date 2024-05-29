//-----------------------------------------------------------------------------
// FILE:        NodeOptions.cs
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
    /// Describes cluster node options.
    /// </summary>
    public class NodeOptions
    {
        private const bool  defaultAllowPackageManagerIPv6 = false;
        private const int   defaultPackageManagerRetries   = 5;

        /// <summary>
        /// Allow the Linux package manager to use IPv6 when communicating with
        /// package mirrors.  This defaults to <c>false</c> to restrict updates
        /// to using IPv4.
        /// </summary>
        [JsonProperty(PropertyName = "AllowPackageManagerIPv6", Required = Required.Default)]
        [YamlMember(Alias = "allowPackageManagerIPv6", ApplyNamingConventions = false)]
        [DefaultValue(defaultAllowPackageManagerIPv6)]
        public bool AllowPackageManagerIPv6 { get; set; } = defaultAllowPackageManagerIPv6;

        /// <summary>
        /// Specifies the number of times the host package manager should retry
        /// failed index or package downloads.  This defaults to <b>5</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PackageManagerRetries", Required = Required.Default)]
        [YamlMember(Alias = "packageManagerRetries", ApplyNamingConventions = false)]
        [DefaultValue(defaultPackageManagerRetries)]
        public int PackageManagerRetries { get; set; } = defaultPackageManagerRetries;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            var optionsPrefix = $"{nameof(NodeOptions)}";

            if (PackageManagerRetries < 0)
            {
                throw new ClusterDefinitionException($"{optionsPrefix}.{nameof(PackageManagerRetries)}={PackageManagerRetries} cannot be negative.");
            }
        }
    }
}
