//-----------------------------------------------------------------------------
// FILE:	    JsonNullableDateTimeConverter.cs
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

namespace Neon.JsonConverters
{
    /// <summary>
    /// Convertes nullable <see cref="DateTime"/> for <see cref="System.Text.Json"/> based serialization.
    /// </summary>
    public class JsonNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        private const string dateFormat = "yyyy-MM-ddTHH:mm:ssZ";

        /// <inheritdoc/>
        public override DateTime? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            Covenant.Requires<ArgumentException>(type == typeof(DateTime?), nameof(type));

            reader.GetString();

            var input = reader.GetString();

            if (input == "null")
            {
                return null;
            }
            else
            {
                return DateTime.ParseExact(input, dateFormat, CultureInfo.InvariantCulture);
            }
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            writer.WriteRawValue(value.HasValue ? value.Value.ToString(dateFormat) : "null");
        }
    }
}
