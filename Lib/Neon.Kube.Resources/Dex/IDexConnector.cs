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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Configuration for backend connectors.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(DexConnectorConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(DexConnectorJsonConverter))]
    public interface IDexConnector
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
        /// Connector Config.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Config", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "config", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        IDexConnectorConfig Config { get; set; }
    }

    /// <summary>
    /// Converter for Dex connectors.
    /// </summary>
    public class DexConnectorConverter : JsonConverter
    {
        /// <summary>
        /// Returns whether the connectio can be converted.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(IDexConnector);
        }

        /// <summary>
        /// Reads the json.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns></returns>
        public override object ReadJson(JsonReader reader,
               Type             objectType, 
               object           existingValue,
               JsonSerializer   serializer)
        {
            var jsonObject = JObject.Load(reader);
            var connector = default(IDexConnector);

            var value = jsonObject.Value<string>("type");
            var type  = NeonHelper.ParseEnum<DexConnectorType>(value);

            switch (type)
            {
                case DexConnectorType.Ldap:

                    connector = new DexLdapConnector();

                    break;

                case DexConnectorType.Oidc:

                    connector = new DexOidcConnector();

                    break;
            }
            
            serializer.Populate(jsonObject.CreateReader(), connector);
            return connector;
        }

        /// <summary>
        /// Writes json.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value);
        }
    }
}