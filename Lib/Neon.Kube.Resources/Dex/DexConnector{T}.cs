//-----------------------------------------------------------------------------
// FILE:        DexOidcConnector.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Neon.Kube.Resources;
using Neon.Kube.Resources.JsonConverters;

using Newtonsoft.Json;
using NJsonSchema.Annotations;
using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for OIDC connectors.
    /// </summary>
    [JsonSchemaAbstract]
    [System.Text.Json.Serialization.JsonConverter(typeof(DexConnectorJsonConverter))]
    public class DexConnector<T> : IDexConnector<T>
        where T : class
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexConnector()
        {
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
        [JsonSchemaExtensionData("x-kubernetes-preserve-unknown-fields", true)]
        public T Config { get; set; }
        object IV1DexConnector.Config { get => (T)Config; set => Config = (T)value; }
    }
}
