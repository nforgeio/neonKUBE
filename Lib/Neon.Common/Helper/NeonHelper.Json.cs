//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Json.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Data;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        // This table is used to cache the factory functions used to create instances of 
        // [IRoundtripData] types.  This must be locked to be threadsafe.

        private static readonly Dictionary<Type, Func<string, object>> typeToGeneratedObjectFactory = new Dictionary<Type, Func<string, object>>();

        /// <summary>
        /// The global <b>relaxed</b> JSON serializer settings.  These settings 
        /// <b>do not require</b> that all source JSON properties match those 
        /// defined by the type being deserialized.
        /// </summary>
        public static Lazy<JsonSerializerSettings> JsonRelaxedSerializerSettings =
            new Lazy<JsonSerializerSettings>(
                () =>
                {
                    var settings = new JsonSerializerSettings();

                    settings.Converters.Add(new StringEnumConverter(new DefaultNamingStrategy(), allowIntegerValues: false));

                    // Ignore missing members for relaxed parsing.

                    settings.MissingMemberHandling = MissingMemberHandling.Ignore;

                    // Allow cyclic data.

                    settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

                    // Serialize dates as UTC like: 2012-07-27T18:51:45.534Z
                    //
                    // The nice thing about this is that Couchbase and other NoSQL database will
                    // be able to do date range queries out-of-the-box.

                    settings.DateFormatHandling   = DateFormatHandling.IsoDateFormat;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

                    // Add standard type converters.

                    AddTypeConverters(settings);

                    return settings;
                });

        /// <summary>
        /// The global <b>strict</b> JSON serializer settings.  These settings 
        /// <b>do require</b> that all source JSON properties match those defined 
        /// by the type being deserialized.
        /// </summary>
        public static Lazy<JsonSerializerSettings> JsonStrictSerializerSettings =
            new Lazy<JsonSerializerSettings>(
                () =>
                {
                    var settings = new JsonSerializerSettings();

                    settings.Converters.Add(new StringEnumConverter(new DefaultNamingStrategy(), allowIntegerValues: false));

                    // Treat missing members as errors for strict parsing.

                    settings.MissingMemberHandling = MissingMemberHandling.Error;

                    // Allow cyclic data.

                    settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

                    // Serialize dates as UTC like: 2012-07-27T18:51:45.534Z
                    //
                    // The nice thing about this is that Couchbase and other NoSQL database will
                    // be able to do date range queries out-of-the-box.

                    settings.DateFormatHandling   = DateFormatHandling.IsoDateFormat;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

                    // Add standard type converters.

                    AddTypeConverters(settings);

                    // Set maximum object nesting depth to mitigate stack overflow/DOS attacks:
                    //
                    //      https://github.com/JamesNK/Newtonsoft.Json/issues/2457

                    settings.MaxDepth = 64;

                    return settings;
                });

        // $todo(jefflill):
        //
        // It would be nice to have a way to detect when the [JsonConverters] is modified
        // after it's too late.  Perhaps using an [ObservableCollection].

        /// <summary>
        /// <para>
        /// Returns the list of <see cref="JsonConverter"/> instances that will be automatically
        /// recognized by the JSON deserializers.  This is initialized with converters for some
        /// common types like <see cref="DateTime"/>, <see cref="TimeSpan"/>, and 
        /// <see cref="Version"/>.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> You may customize this list but for that to have any impact,
        /// you must make the modifications <b>very early</b> in your application startup sequence,
        /// <b>before any JSON serialization operations</b> have been performed.  Any changes
        /// made after this will be ignored.
        /// </note>
        /// </summary>
        public static List<JsonConverter> JsonConverters { get; private set; } =
            new List<JsonConverter>()
            {
                new DateTimeJsonConverter(),
                new DateTimeOffsetJsonConverter(),
                new TimeSpanJsonConverter(),
                new VersionJsonConverter()
            };

        /// <summary>
        /// Adds the standard type converters to serializer settings.
        /// </summary>
        /// <param name="settings">The target settings.</param>
        private static void AddTypeConverters(JsonSerializerSettings settings)
        {
            foreach (var converter in JsonConverters)
            {
                settings.Converters.Add(converter);
            }
        }

        /// <summary>
        /// Serializes an object to JSON text.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <returns>The JSON text.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static string JsonSerialize(object value, Formatting format = Formatting.None)
        {
            return JsonConvert.SerializeObject(value, format, JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Serializes an object to UTF-8 encoded JSON bytes.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <returns>The UTF-8 encoded JSON bytes.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static byte[] JsonSerializeToBytes(object value, Formatting format = Formatting.None)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, format, JsonRelaxedSerializerSettings.Value));
        }

        /// <summary>
        /// Deserializes JSON text, optionally requiring strict mapping of input properties to the target type.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="json">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to <typeparamref name="T"/> properties.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static T JsonDeserialize<T>(string json, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(json != null, nameof(json));

            return JsonConvert.DeserializeObject<T>(json, strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Deserializes UITF-8 encoded JSON bytes, optionally requiring strict mapping of input properties to the target type.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="jsonBytes">The UTF-8 encoded JSON bytes.</param>
        /// <param name="strict">Optionally require that all input properties map to <typeparamref name="T"/> properties.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static T JsonDeserialize<T>(byte[] jsonBytes, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(jsonBytes != null, nameof(jsonBytes));

            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(jsonBytes), strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Non-generic method that deserializes JSON text, optionally requiring strict mapping of input properties to the target type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="json">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to <paramref name="type"/> properties.</param>
        /// <returns>The parsed <c>object</c>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static object JsonDeserialize(Type type, string json, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(json != null, nameof(json));

            return JsonConvert.DeserializeObject(json, type, strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Non-generic method that deserializes UTF-8 encoded JSON bytes, optionally requiring strict mapping of input properties to the target type.
        /// </summary>
        /// <param name="type">The target type.</param>
        /// <param name="jsonBytes">The UTF-8 encoded JSON bytes.</param>
        /// <param name="strict">Optionally require that all input properties map to <paramref name="type"/> properties.</param>
        /// <returns>The parsed <c>object</c>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static object JsonDeserialize(Type type, byte[] jsonBytes, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
            Covenant.Requires<ArgumentNullException>(jsonBytes != null, nameof(jsonBytes));

            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(jsonBytes), type, strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Serializes an object to JSON text using custom settings.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <param name="settings">The optional settings or <c>null</c> to use <see cref="JsonStrictSerializerSettings"/>.</param>
        /// <returns>The JSON text.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static string JsonSerialize(object value, JsonSerializerSettings settings, Formatting format = Formatting.None)
        {
            return JsonConvert.SerializeObject(value, format, settings ?? JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Serializes an object to UTF-8 encoded JSON bytes using custom settings.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <param name="settings">The optional settings or <c>null</c> to use <see cref="JsonStrictSerializerSettings"/>.</param>
        /// <returns>The ITF-8 encoded JSON bytes.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static byte[] JsonSerializeToBytes(object value, JsonSerializerSettings settings, Formatting format = Formatting.None)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value, format, settings ?? JsonRelaxedSerializerSettings.Value));
        }

        /// <summary>
        /// Deserializes JSON text using custom settings.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="json">The JSON text.</param>
        /// <param name="settings">The optional settings or <c>null</c> to use <see cref="JsonRelaxedSerializerSettings"/>.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonRelaxedSerializerSettings"/> when specific
        /// settings are not passed.  You may pass <see cref="JsonStrictSerializerSettings"/> or
        /// entirely custom settings.
        /// </remarks>
        public static T JsonDeserialize<T>(string json, JsonSerializerSettings settings)
        {
            return JsonConvert.DeserializeObject<T>(json, settings ?? JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Creates a deep clone of an object by first serializing to JSON and then
        /// deserializing it.
        /// </summary>
        /// <typeparam name="T">The object type.</typeparam>
        /// <param name="value">The object being clonned or <c>null</c>.</param>
        /// <returns>The clone.</returns>
        public static T JsonClone<T>(T value)
            where T : class
        {
            if (value == null)
            {
                return null;
            }

            return JsonDeserialize<T>(JsonSerialize(value));
        }

        /// <summary>
        /// Compares two object instances for equality by serializing them JSON and
        /// comparing the output.
        /// </summary>
        /// <param name="v1">Value 1</param>
        /// <param name="v2">Value 2</param>
        /// <returns><c>true</c> if the instances are the same.</returns>
        /// <remarks>
        /// This is a convienent and safe way of comparing two objects without having
        /// to comparing a potentially complex tree of members and then maintaining
        /// that as code changes over time at the cost of having to perform the
        /// serializations.
        /// </remarks>
        public static bool JsonEquals(object v1, object v2)
        {
            // Optimize some common scenarios.

            if (object.ReferenceEquals(v1, v2))
            {
                return true;
            }

            var v1Null = (object)v1 == null;
            var v2Null = (object)v2 == null;

            if (v1Null && !v1Null || !v1Null && v2Null)
            {
                return false;
            }

            // The instances are not NULL and are not the same so serialize
            // and compare.

            return JsonSerialize(v1, Formatting.None) == JsonSerialize(v2, Formatting.None);
        }
    }
}
