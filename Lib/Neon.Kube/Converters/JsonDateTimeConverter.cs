//-----------------------------------------------------------------------------
// FILE:	    JsonDateTimeConverter.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Converts generic types using JSON.NET.
    /// </summary>
    public class JsonDateTimeConverter : JsonConverter<DateTime>
    {
        private const string dateFormat = "yyyy-MM-ddTHH:mm:ssZ";

        /// <inheritdoc/>
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            reader.GetString();

            var input = reader.GetString();

            // $debug(jefflill)

            Console.WriteLine();
            Console.WriteLine("###########################################");
            Console.WriteLine($"input: {input}");
            Console.WriteLine("###########################################");
            Console.WriteLine();

            return DateTime.ParseExact(input, dateFormat, CultureInfo.InvariantCulture);
        }

        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            // $debug(jefflill)

            Console.WriteLine();
            Console.WriteLine("###########################################");
            Console.WriteLine($"output: {value.ToString(dateFormat)}");
            Console.WriteLine("###########################################");
            Console.WriteLine();

            writer.WriteRawValue(value.ToString(dateFormat));
        }
    }
}
