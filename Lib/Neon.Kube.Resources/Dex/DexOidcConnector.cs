//-----------------------------------------------------------------------------
// FILE:	    DexOidcConnector.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Configuration for OIDC connectors.
    /// </summary>
    public class DexOidcConnector : IDexConnector
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexOidcConnector()
        {
            Type = DexConnectorType.Oidc;
        }

        /// <inheritdoc/>
        public string Id { get; set; }


        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public DexConnectorType Type { get; set; }

        /// <summary>
        /// Connector specific config.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Config", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "config", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexOidcConfig Config { get; set; }
    }
}