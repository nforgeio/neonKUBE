//-----------------------------------------------------------------------------
// FILE:        V1ResourceConverter.cs
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube.Resources.JsonConverters
{
    /// <summary>
    /// Json collection converter.
    /// </summary>
    /// <typeparam name="TDatatype">Type of item to convert.</typeparam>
    /// <typeparam name="TConverterType">Converter to use for individual items.</typeparam>
    public class JsonCollectionItemConverter<TDatatype, TConverterType> : JsonConverter<IEnumerable<TDatatype>>
        where TConverterType : JsonConverter
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            var result = objectType == typeof(IEnumerable<TDatatype>);

            return result;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, IEnumerable<TDatatype> value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            var jsonSerializerOptions = new JsonSerializerOptions(options);

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
