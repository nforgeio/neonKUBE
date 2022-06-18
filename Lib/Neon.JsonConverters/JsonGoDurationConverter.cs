//-----------------------------------------------------------------------------
// FILE:	    JsonGoDurationConverter.cs
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
using System.Diagnostics.Contracts;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Time;

namespace Neon.JsonConverters
{
    /// <summary>
    /// Converts <see cref="TimeSpan"/> serialized as a <see cref="GoDuration"/> for <see cref="System.Text.Json"/> based serialization.
    /// </summary>
    public class JsonGoDurationConverter : JsonConverter<TimeSpan>
    {
        /// <inheritdoc/>
        public override TimeSpan Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            Covenant.Requires<ArgumentException>(type == typeof(DateTime), nameof(type));

            reader.GetString();

            var input = reader.GetString();

            return GoDuration.Parse(input).TimeSpan;
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(GoDuration.FromTimeSpan(value).ToPretty());
        }
    }
}
