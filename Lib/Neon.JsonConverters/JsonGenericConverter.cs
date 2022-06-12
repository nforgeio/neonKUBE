//-----------------------------------------------------------------------------
// FILE:	    JsonGenericConverter.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;

// $todo(jefflill):
//
// We shouldn't be relying on JSON.NET here.
//
//      https://github.com/nforgeio/neonKUBE/issues/1587

namespace Neon.JsonConverters
{
    /// <summary>
    /// Converts generic types for <see cref="System.Text.Json"/> based serialization.
    /// </summary>
    public class JsonGenericConverter<T> : JsonConverter<T>
    {
        /// <inheritdoc/>
        public override T Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            reader.GetString();

            return NeonHelper.JsonDeserialize<T>(reader.GetString());
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var stringValue = NeonHelper.JsonSerialize(value);

            writer.WriteRawValue(stringValue);
        }
    }
}
