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

namespace Neon.Kube.Resources
{
    /// <summary>
    /// <see cref="System.Text.Json"/>: Converts <see cref="V1ResourceRequirements"/>.
    /// </summary>
    public class JsonV1ResourceConverter : JsonConverter<V1ResourceRequirements>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(V1ResourceRequirements);
        }

        /// <inheritdoc/>
        public override V1ResourceRequirements Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            // initialize
            var result                = new V1ResourceRequirements();
            result.Limits             = new Dictionary<string, ResourceQuantity>();
            result.Requests           = new Dictionary<string, ResourceQuantity>();
            result.Limits["cpu"]      = new ResourceQuantity();
            result.Limits["memory"]   = new ResourceQuantity();
            result.Requests["cpu"]    = new ResourceQuantity();
            result.Requests["memory"] = new ResourceQuantity();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                // Get the key.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();
                var value        = (JsonElement)JsonSerializer.Deserialize<dynamic>(ref reader, options);

                if (propertyName == "limits")
                {
                    if (value.TryGetProperty("cpu", out var cpu))
                    {
                        result.Limits["cpu"].Value = cpu.GetString();
                    }
                    if (value.TryGetProperty("memory", out var memory))
                    {
                        result.Limits["memory"].Value = memory.GetString();
                    }
                }

                if (propertyName == "requests")
                {
                    if (value.TryGetProperty("cpu", out var cpu))
                    {
                        result.Requests["cpu"].Value = cpu.GetString();
                    }
                    if (value.TryGetProperty("memory", out var memory))
                    {
                        result.Requests["memory"].Value = memory.GetString();
                    }
                }
            }

            throw new JsonException();
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, V1ResourceRequirements value, JsonSerializerOptions options)
        {
            var stringValue = NeonHelper.JsonSerialize(value);

            writer.WriteRawValue(stringValue);
        }
    }
}
