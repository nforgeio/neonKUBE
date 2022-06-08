//-----------------------------------------------------------------------------
// FILE:	    IntegerEnumConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Neon.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Data
{
    /// <summary>
    /// <b>Newtonsoft:</b> Implements a type converter that converts between integers and an enum type.
    /// </summary>
    /// <typeparam name="TEnum">The enumation type being converted.</typeparam>
    /// <remarks>
    /// <note>
    /// This works for both string and integer values and we don't ensure that 
    /// an integer input value actually corresponds to an enum value, we just cast
    /// the integer.
    /// </note>
    /// </remarks>
    public class IntegerEnumConverter<TEnum> : JsonConverter
        where TEnum : struct, Enum
    {
        /// <summary>
        /// Determines whether the converter is able to convert a value of a specific type.
        /// </summary>
        /// <param name="objectType">The value type.</param>
        /// <returns><c>true</c> if the type can be converted.</returns>
        /// <inheritdoc/>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string) || objectType == typeof(int);
        }

        /// <summary>
        /// Writes an integer or enum value as an integer.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <param name="value">The value.</param>
        /// <param name="serializer">The serializer.</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="serializer"/> parameter is ignored.
        /// </note>
        /// </remarks>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteValue(0);
                return;
            }

            var type = value.GetType();

            if (type == typeof(long))
            {
                writer.WriteValue((long)value);
            }
            else if (type == typeof(int))
            {
                writer.WriteValue((int)value);
            }
            else if (type == typeof(short))
            {
                writer.WriteValue((short)value);
            }
            else if (type == typeof(byte))
            {
                writer.WriteValue((byte)value);
            }
            else if (type == typeof(TEnum))
            {
                writer.WriteValue(Convert.ToInt64(value));
            }
            else
            {
                throw new JsonSerializationException($"Unexpected value type [{type.FullName}].  A string or integer is expected.");
            }
        }

        /// <summary>
        /// Reads an integer or enum value as a enum.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="objectType">The value type.</param>
        /// <param name="existingValue">The existing value.</param>
        /// <param name="serializer">The serializer.</param>
        /// <returns>The value read.</returns>
        /// <remarks>
        /// <note>
        /// The <paramref name="serializer"/> parameter is ignored.
        /// </note>
        /// </remarks>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            Covenant.Assert(objectType == typeof(TEnum));

            if (reader.TokenType == JsonToken.Integer)
            {
                return (TEnum)(ValueType)Convert.ToInt32(reader.Value);
            }
            else if (reader.TokenType == JsonToken.String)
            {
                return NeonHelper.ParseEnum<TEnum>((string)reader.Value);
            }
            else
            {
                throw new JsonSerializationException($"Cannot convert [{reader.TokenType}] to [{typeof(TEnum).FullName}].");
            }
        }
    }
}
