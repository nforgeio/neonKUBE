//-----------------------------------------------------------------------------
// FILE:	    IDexConnector.cs
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

using Neon.Common;
using Neon.Kube.Resources;
using Neon.Kube.Resources.JsonConverters;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NJsonSchema.Annotations;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for backend connectors.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(DexConnectorConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(DexConnectorJsonConverter))]
    public interface IDexConnector<T> : IV1DexConnector
        where T : class
    {
        /// <summary>
        /// Connector ID
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        new string Id { get; set; }

        /// <summary>
        /// Connector name.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        new string Name { get; set; }

        /// <summary>
        /// Connector Type.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "type", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        new DexConnectorType Type { get; set; }

        /// <summary>
        /// Connector config.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Config", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "config", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        [JsonSchemaExtensionData("x-kubernetes-preserve-unknown-fields", true)]
        new T Config { get; set; }
    }
}