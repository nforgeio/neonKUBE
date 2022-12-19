//-----------------------------------------------------------------------------
// FILE:	    V1ResourceConverter.cs
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube.Resources.Dex;

using k8s;
using k8s.Models;

namespace Neon.Kube.Resources.JsonConverters
{
    /// <summary>
    /// Converter for Dex connectors.
    /// </summary>
    public class DexConnectorJsonConverter : JsonConverter<IDexConnector>
    {
        /// <summary>
        /// Returns whether the connectio can be converted.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            var result = objectType == typeof(IDexConnector)
                || objectType.Implements<IDexConnector>();

            return result;
        }

        /// <inheritdoc/>

        public override IDexConnector Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)

        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            Utf8JsonReader readerClone = reader;

            var result = default(IDexConnector);

            DexConnectorType? type = null;
            int depth = 0;

            while (readerClone.Read() 
                && !type.HasValue)
            {
                JsonTokenType tokenType = readerClone.TokenType;

                switch (tokenType)
                {
                    case JsonTokenType.StartObject:
                        depth++;
                        continue;

                    case JsonTokenType.EndObject:
                        depth--;
                        continue;
                }

                if (tokenType == JsonTokenType.EndObject
                    && depth == 0)
                {
                    return result;
                }

                // Get the key.
                if (tokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = readerClone.GetString();

                    if (propertyName == "type" && depth == 0)
                    {
                        var value = (JsonElement)JsonSerializer.Deserialize<dynamic>(ref readerClone, options);
                        var str = value.ToString();
                        type = NeonHelper.ParseEnum<DexConnectorType>(value.ToString());
                    }
                }
            }
            

            switch (type)
            {
                case DexConnectorType.Ldap:
                    result = new DexConnector<DexLdapConfig>();
                    break;


                case DexConnectorType.Oidc:
                    result = new DexConnector<DexOidcConfig>();
                    break;
            }

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propertyName = reader.GetString();
                    reader.Read();
                    switch (propertyName)
                    {
                        case "id":
                            result.Id = reader.GetString();
                            break;
                        case "name":
                            result.Name = reader.GetString();

                            break;
                        case "type":
                            result.Type = type.Value;
                            break;

                        case "config":

                            switch (type)
                            {
                                case DexConnectorType.Ldap:

                                    ((DexConnector<DexLdapConfig>)result).Config = JsonSerializer.Deserialize<DexLdapConfig>(ref reader, options);

                                    break;

                                case DexConnectorType.Oidc:

                                    ((DexConnector<DexOidcConfig>)result).Config = JsonSerializer.Deserialize<DexOidcConfig>(ref reader, options);

                                    break;
                            }
                            break;
                    }
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IDexConnector value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            writer.WriteString("id", value.Id);
            writer.WriteString("name", value.Name);
            writer.WriteString("type", value.Type.ToMemberString());

            writer.WritePropertyName("config");

            if (value is IDexConnector<DexOidcConfig> oidcConfig)
            {
                JsonSerializer.Serialize(writer, oidcConfig.Config, options);
            }
            else if (value is IDexConnector<DexLdapConfig> ldapConfig)
            {
                JsonSerializer.Serialize(writer, ldapConfig.Config, options);
            }

            writer.WriteEndObject();
        }
    }
}
