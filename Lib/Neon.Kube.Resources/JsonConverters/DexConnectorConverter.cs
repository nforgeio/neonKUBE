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

using k8s;
using k8s.Models;
using IdentityModel.OidcClient;
using System.Text.Json.Nodes;

namespace Neon.Kube.Resources
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
            return objectType == typeof(IDexConnector);
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

            while (readerClone.Read() 
                && type != null)
            {
                if (readerClone.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                // Get the key.
                if (readerClone.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = readerClone.GetString();

                if (propertyName == "type")
                {
                    var value = (JsonElement)JsonSerializer.Deserialize<dynamic>(ref readerClone, options);
                    var str = value.ToString();
                    type = NeonHelper.ParseEnum<DexConnectorType>(value.ToString());
                }
            }

            switch (type)
            {
                case DexConnectorType.Ldap:

                    result = JsonSerializer.Deserialize<DexLdapConnector>(ref reader);

                    break;

                case DexConnectorType.Oidc:

                    result = JsonSerializer.Deserialize<DexOidcConnector>(ref reader);

                    break;
            }

            return result;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IDexConnector value, JsonSerializerOptions options)
        {
            var stringValue = NeonHelper.JsonSerialize(value);

            writer.WriteRawValue(stringValue);
        }
    }
}
