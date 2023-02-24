//-----------------------------------------------------------------------------
// FILE:	    IV1DexConnector.cs
// CONTRIBUTOR: Marcus Bowyer
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
    public interface IV1DexConnector
    {
        /// <summary>
        /// Connector ID
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        string Id { get; set; }

        /// <summary>
        /// Connector name.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        string Name { get; set; }

        /// <summary>
        /// Connector Type.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "type", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        DexConnectorType Type { get; set; }

        /// <summary>
        /// Connector config.
        /// information.
        /// </summary>
        [JsonSchemaExtensionData("x-kubernetes-preserve-unknown-fields", true)]
        object Config { get; set; }
    }

    /// <summary>
    /// Converter for Dex connectors.
    /// </summary>
    public class DexConnectorConverter : JsonConverter
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            Covenant.Requires<ArgumentNullException>(objectType != null, nameof(objectType));

            return objectType == typeof(IV1DexConnector) || objectType.Implements<IV1DexConnector>();
        }

        /// <summary>
        /// Returns the Dex connector from JSON read from a reader.
        /// </summary>
        /// <param name="reader">Specifies the source JSON reader.</param>
        /// <param name="objectType">Specifies the object type (ignored).</param>
        /// <param name="existingValue">Specifies the existing value (ignored).</param>
        /// <param name="serializer">Specifies the JSON serializer.</param>
        /// <returns>The Dex connector.</returns>
        public override object ReadJson(
            JsonReader       reader,
            Type             objectType, 
            object           existingValue,
            JsonSerializer   serializer)
        {
            Covenant.Requires<ArgumentNullException>(reader != null, nameof(reader));
            Covenant.Requires<ArgumentNullException>(serializer != null, nameof(serializer));

            var jsonObject = JObject.Load(reader);
            var connector  = default(IV1DexConnector);
            var value      = jsonObject.Value<string>("type");
            var type       = NeonHelper.ParseEnum<DexConnectorType>(value);

            switch (type)
            {
                case DexConnectorType.Ldap:

                    connector = new DexConnector<DexLdapConfig>();
                    break;

                case DexConnectorType.Oidc:

                    connector = new DexConnector<DexOidcConfig>();
                    break;
            }
            
            serializer.Populate(jsonObject.CreateReader(), connector);

            return connector;
        }

        /// <summary>
        /// Writes the connection information to a JSON writer.
        /// </summary>
        /// <param name="writer">Specifies the target JSON writer.</param>
        /// <param name="value">Specifies the  connection object.</param>
        /// <param name="serializer">Specifies the JSON serializer.</param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Covenant.Requires<ArgumentNullException>(writer != null, nameof(writer));
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));
            Covenant.Requires<ArgumentNullException>(serializer != null, nameof(serializer));
            
            serializer.Serialize(writer, value);
        }
    }
}