//-----------------------------------------------------------------------------
// FILE:	    SerializationHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

namespace Neon.Serialization
{
    /// <summary>
    /// Serialization related helpers used by the code generated
    /// by the <b>Neon.CodeGen</b> library.
    /// </summary>
    public static class SerializationHelper
    {
        private static readonly JsonSerializerSettings settings;

        /// <summary>
        /// Error message used when <see cref="object.GetHashCode()"/> is called on
        /// a generated data model that has bo properties tagged with [HashSource].
        /// </summary>
        public const string NoHashPropertiesError = "At least one data model property must be tagged by [HashSourceAttribute].";

        /// <summary>
        /// Returns the Json serializer.
        /// </summary>
        public static JsonSerializer Serializer { get; private set; }

        static SerializationHelper()
        {
            settings = new JsonSerializerSettings()
            {
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DateFormatHandling    = DateFormatHandling.IsoDateFormat,
                DateTimeZoneHandling  = DateTimeZoneHandling.Utc
            };

            settings.Converters.Add(new StringEnumConverter(new DefaultNamingStrategy(), allowIntegerValues: false));

            Serializer = JsonSerializer.Create(settings);
        }

        /// <summary>
        /// Serializes a value to JSON text.
        /// </summary>
        /// <param name="value">The the value to be serialized.</param>
        /// <param name="format">Optionally format the output.</param>
        /// <returns>The JSON text.</returns>
        public static string Serialize(object value, Formatting format = Formatting.None)
        {
            return JsonConvert.SerializeObject(value, format, settings);
        }

        /// <summary>
        /// Deserializes a value from JSON text.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="jsonText">The JSON text.</param>
        /// <returns>The deserialized value.</returns>
        public static T Deserialize<T>(string jsonText)
        {
            return JsonConvert.DeserializeObject<T>(jsonText, settings);
        }

        /// <summary>
        /// Used to convert a value into a <see cref="JToken"/> suitable for
        /// assigning as a property to the backing <see cref="JObject"/> of
        /// a generated data model
        /// </summary>
        /// <param name="value">The value being assigned.</param>
        /// <param name="objectType">The generated data model type.</param>
        /// <param name="propertyName">The property name.</param>
        /// <returns>The <see cref="JToken"/>.</returns>
        public static JToken FromObject(object value, Type objectType, string propertyName)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return JToken.FromObject(value, Serializer);
            }
            catch (JsonSerializationException e)
            {
                throw new SerializationException($"Error persisting value to [{objectType.Name}.{propertyName}]: {e.Message}", e);
            }
        }
    }
}
