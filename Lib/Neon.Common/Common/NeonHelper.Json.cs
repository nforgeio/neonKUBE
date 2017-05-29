//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Csv.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
        /// The global JSON serializer settings.  These settings serialize enumerations as
        /// non-camel case strings, not integers for better cross language compatibility.
        /// </summary>
        public static JsonSerializerSettings JsonSerializerSettings { get; set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonHelper()
        {
            JsonSerializerSettings = new JsonSerializerSettings();
            JsonSerializerSettings.Converters.Add(
                new StringEnumConverter(false)
                {
                    AllowIntegerValues = false
                });
        }

        /// <summary>
        /// Serializes an object to JSON text using optional settings.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <param name="settings">The optional settings or <c>null</c> to use <see cref="JsonSerializerSettings"/>.</param>
        /// <returns>The JSON text.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonSerializerSettings"/> if when specific
        /// settings are not passed.  These settings serialize enumerations as
        /// non-camel case strings, not integers for better cross language compatibility.
        /// </remarks>
        public static string JsonSerialize(object value, Formatting format = Formatting.None, JsonSerializerSettings settings = null)
        {
            return JsonConvert.SerializeObject(value, format, settings ?? JsonSerializerSettings);
        }

        /// <summary>
        /// Deserializes JSON text using optional settings.
        /// </summary>
        /// <typeparam name="TObject">The desired output type.</typeparam>
        /// <param name="json">The JSON text.</param>
        /// <param name="settings">The optional settings or <c>null</c> to use <see cref="JsonSerializerSettings"/>.</param>
        /// <returns>The parsed <typeparamref name="TObject"/>.</returns>
        /// <remarks>
        /// This method uses the default <see cref="JsonSerializerSettings"/> if when specific
        /// settings are not passed.  These settings deserialize enumerations as
        /// non-camel case strings, not integers for better cross language compatibility.
        /// </remarks>
        public static TObject JsonDeserialize<TObject>(string json, JsonSerializerSettings settings = null)
        {
            return JsonConvert.DeserializeObject<TObject>(json, settings ?? JsonSerializerSettings);
        }

        /// <summary>
        /// Creates a deep clone of an object by first serializing to JSON and then
        /// deserializing it.
        /// </summary>
        /// <typeparam name="TObject">The object type.</typeparam>
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
    }
}
