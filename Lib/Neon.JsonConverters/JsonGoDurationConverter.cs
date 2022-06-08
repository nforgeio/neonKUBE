//-----------------------------------------------------------------------------
// FILE:	    JsonGoDurationConverter.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

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
            writer.WriteRawValue(GoDuration.FromTimeSpan(value).ToString());
        }
    }
}
