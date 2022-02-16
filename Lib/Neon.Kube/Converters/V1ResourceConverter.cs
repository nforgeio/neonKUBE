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
            var value     = reader.GetString()!;
            var resources = NeonHelper.JsonDeserialize<dynamic>(reader.GetString());
            var result    = new V1ResourceRequirements();

            result.Requests = new Dictionary<string, ResourceQuantity>();
            result.Limits   = new Dictionary<string, ResourceQuantity>();

            result.Requests["cpu"]    = new ResourceQuantity(resources.limits.cpu);
            result.Requests["memory"] = new ResourceQuantity(resources.limits.cpu);

            result.Limits["cpu"]      = new ResourceQuantity(resources.limits.cpu);
            result.Limits["memory"]   = new ResourceQuantity(resources.limits.cpu);

            return result;
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
