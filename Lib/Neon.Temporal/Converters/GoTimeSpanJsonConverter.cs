//-----------------------------------------------------------------------------
// FILE:	    GoTimeSpanJsonConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using Neon.Data;
using Neon.Time;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Temporal
{
    /// <summary>
    /// <para>
    /// Implements a type converter for <see cref="TimeSpan"/> using the culture
    /// Go Language formatted time.  This serializes <see cref="TimeSpan"/> instances
    /// in nanoseconds as a long.  The maximum <see cref="TimeSpan"/> that this converter
    /// can accurately handle is about 290 year.  Any amount of time larger than 290 years
    /// is not suitable for this serializer.
    /// </para>
    /// </summary>
    public class GoTimeSpanJsonConverter : JsonConverter<TimeSpan>, IEnhancedJsonConverter
    {
        private string format = "c";

        /// <inheritdoc/>
        public Type Type => typeof(TimeSpan);

        /// <inheritdoc/>
        public override TimeSpan ReadJson(JsonReader reader, Type objectType, TimeSpan existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return TimeSpan.FromTicks((long)reader.Value / 100L);
        }

        /// <inheritdoc/>
        public override void WriteJson(JsonWriter writer, TimeSpan value, JsonSerializer serializer)
        {
            writer.WriteValue(GoTimeSpan.FromTimeSpan(value).Ticks);
        }

        /// <inheritdoc/>
        public string ToSimpleString(object instance)
        {
            Covenant.Requires<ArgumentNullException>(instance != null, nameof(instance));

            return ((TimeSpan)instance).ToString(format);
        }
    }
}
