//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Json.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
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

                    settings.Converters.Add(
                        new StringEnumConverter(false)
                        {
                            AllowIntegerValues = false
                        });

                    // Ignore missing members for relaxed parsing.

                    settings.MissingMemberHandling = MissingMemberHandling.Ignore;

                    // Serialize dates as UTC like: 2012-07-27T18:51:45.53403Z
                    //
                    // The nice thing about this is that Couchbase and other NoSQL database will
                    // be able to do date range queries out-of-the-box.

                    settings.DateFormatHandling   = DateFormatHandling.IsoDateFormat;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

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

                    settings.Converters.Add(
                        new StringEnumConverter(false)
                        {
                            AllowIntegerValues = false
                        });

                    // Treat missing members as errors for strict parsing.

                    settings.MissingMemberHandling = MissingMemberHandling.Error;

                    // Serialize dates as UTC like: 2012-07-27T18:51:45.53403Z
                    //
                    // The nice thing about this is that Couchbase and other NoSQL database will
                    // be able to do date range queries out-of-the-box.

                    settings.DateFormatHandling   = DateFormatHandling.IsoDateFormat;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

                    return settings;
                });

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
        /// Deserializes JSON text optionally requiring strict mapping of input properties to the target type.
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
            return JsonConvert.DeserializeObject<T>(json, strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
        }

        /// <summary>
        /// Non-generic method that deserializes JSON text optionally requiring strict mapping of input properties to the target type.
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
            Covenant.Requires<ArgumentNullException>(type != null);

            return JsonConvert.DeserializeObject(json, type, strict ? JsonStrictSerializerSettings.Value : JsonRelaxedSerializerSettings.Value);
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
