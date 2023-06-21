//-----------------------------------------------------------------------------
// FILE:        KubeClientConfig.cs
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
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Used by the NEONKUBE client desktop and command line tools to 
    /// manage the client side configuration.
    /// </summary>
    public class KubeClientConfig
    {
        /// <summary>
        /// Default constuctor.
        /// </summary>
        public KubeClientConfig()
        {
        }

        /// <summary>
        /// The schema version for this state file.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "schema", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Schema { get; set; } = "v1";

        /// <summary>
        /// The globally unique client installation ID.
        /// </summary>
        [JsonProperty(PropertyName = "InstallationId", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "installationId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstallationId { get; set; }

        /// <summary>
        /// Ensures that the state is valid.
        /// </summary>
        public void Validate()
        {
            // Generate a new installation ID if we don't have one or it's invalid.

            if (string.IsNullOrEmpty(InstallationId) || !Guid.TryParse(InstallationId, out var guid))
            {
                InstallationId = Guid.NewGuid().ToString("d");
            }
        }
    }
}
