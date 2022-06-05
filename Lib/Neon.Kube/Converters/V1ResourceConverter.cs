//-----------------------------------------------------------------------------
// FILE:	    V1ResourceConverter.cs
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
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using Neon.Common;

using k8s;
using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// A JSON converter for converting generic types using JSON.NET.
    /// </summary>
    public class V1ResourceConverter : JsonConverter<V1ResourceRequirements>
    {
        /// <inheritdoc/>
        public override bool CanConvert(Type typeToConvert)
        {
            return typeToConvert == typeof(V1ResourceRequirements);
        }

        /// <inheritdoc/>
        public override V1ResourceRequirements Read(
            ref Utf8JsonReader      reader,
            Type                    typeToConvert,
            JsonSerializerOptions   options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            // initialize
            var result                = new V1ResourceRequirements();
            result.Limits             = new Dictionary<string, ResourceQuantity>();
            result.Requests           = new Dictionary<string, ResourceQuantity>();
            result.Limits["cpu"]      = new ResourceQuantity();
            result.Limits["memory"]   = new ResourceQuantity();
            result.Requests["cpu"]    = new ResourceQuantity();
            result.Requests["memory"] = new ResourceQuantity();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return result;
                }

                // Get the key.
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var propertyName = reader.GetString();
                var value        = (JsonElement)JsonSerializer.Deserialize<dynamic>(ref reader, options);

                if (propertyName == "limits")
                {
                    if (value.TryGetProperty("cpu", out var cpu))
                    {
                        result.Limits["cpu"].Value = cpu.GetString();
                    }
                    if (value.TryGetProperty("memory", out var memory))
                    {
                        result.Limits["memory"].Value = memory.GetString();
                    }
                }

                if (propertyName == "requests")
                {
                    if (value.TryGetProperty("cpu", out var cpu))
                    {
                        result.Requests["cpu"].Value = cpu.GetString();
                    }
                    if (value.TryGetProperty("memory", out var memory))
                    {
                        result.Requests["memory"].Value = memory.GetString();
                    }
                }
            }

            throw new JsonException();
        }

        /// <inheritdoc/>
        public override void Write(
            Utf8JsonWriter          writer,
            V1ResourceRequirements  value,
            JsonSerializerOptions   options)
        {
            var stringValue = NeonHelper.JsonSerialize(value);

            writer.WriteRawValue(stringValue);
        }
    }
}
