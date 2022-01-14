//-----------------------------------------------------------------------------
// FILE:	    JsonDynamicConverter.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// The contents of this repository are for private use by neonFORGE, LLC. and may not be
// divulged or used for any purpose by other organizations or individuals without a
// formal written and signed agreement with neonFORGE, LLC.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// A JSON converter for converting generic types using JSON.NET.
    /// </summary>
    public class JsonGenericConverter<T> : System.Text.Json.Serialization.JsonConverter<T>
    {
        /// <inheritdoc/>
        public override T Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            string value = reader.GetString()!;

            return NeonHelper.JsonDeserialize<T>(reader.GetString());
        }

        /// <inheritdoc/>
        public override void Write(
            Utf8JsonWriter writer,
            T value,
            JsonSerializerOptions options)
        {
            var stringValue = NeonHelper.JsonSerialize(value);
            writer.WriteRawValue(stringValue);
        }
    }
}
