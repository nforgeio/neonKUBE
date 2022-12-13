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
    /// Json collection converter.
    /// </summary>
    /// <typeparam name="TDatatype">Type of item to convert.</typeparam>
    /// <typeparam name="TConverterType">Converter to use for individual items.</typeparam>
    public class JsonCollectionItemConverter<TDatatype, TConverterType> : JsonConverter<IEnumerable<TDatatype>>
        where TConverterType : JsonConverter
    {
        /// <summary>
        /// Returns whether the connectio can be converted.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns></returns>
        public override bool CanConvert(Type objectType)
        {
            var result = objectType == typeof(IEnumerable<TDatatype>);

            return result;
        }

        /// <summary>
        /// Reads a json string and deserializes it into an object.
        /// </summary>
        /// <param name="reader">Json reader.</param>
        /// <param name="typeToConvert">Type to convert.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>Created object.</returns>
        public override IEnumerable<TDatatype> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return default(IEnumerable<TDatatype>);
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(options);
            jsonSerializerOptions.Converters.Clear();
            jsonSerializerOptions.Converters.Add(Activator.CreateInstance<TConverterType>());

            List<TDatatype> returnValue = new List<TDatatype>();

            while (reader.TokenType != JsonTokenType.EndArray)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    returnValue.Add((TDatatype)JsonSerializer.Deserialize(ref reader, typeof(TDatatype), jsonSerializerOptions));
                }

                reader.Read();
            }

            return returnValue;
        }

        /// <summary>
        /// Writes a json string.
        /// </summary>
        /// <param name="writer">Json writer.</param>
        /// <param name="value">Value to write.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, IEnumerable<TDatatype> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions(options);
            jsonSerializerOptions.Converters.Clear();
            jsonSerializerOptions.Converters.Add(Activator.CreateInstance<TConverterType>());

            writer.WriteStartArray();

            foreach (TDatatype data in value)
            {
                JsonSerializer.Serialize(writer, data, jsonSerializerOptions);
            }

            writer.WriteEndArray();
        }
    }
}
