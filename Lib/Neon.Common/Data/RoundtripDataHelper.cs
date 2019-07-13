//-----------------------------------------------------------------------------
// FILE:	    RoundtripDataHelper.cs
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
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Data
{
    /// <summary>
    /// Serialization related helpers used by the code generated
    /// by the <b>Neon.CodeGen</b> library.
    /// </summary>
    public static class RoundtripDataHelper
    {
        private static readonly JsonSerializerSettings  settings;
        private static bool                             persistablesInitialzed;

        /// <summary>
        /// The error message used when <see cref="object.GetHashCode()"/> is called on
        /// a generated data model that has bo properties tagged with [HashSource].
        /// </summary>
        public const string NoHashPropertiesError = "At least one data model property must be tagged by [HashSourceAttribute].";

        /// <summary>
        /// Returns the Json global serializer.
        /// </summary>
        public static JsonSerializer Serializer { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static RoundtripDataHelper()
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
        /// <para>
        /// This examines all loaded assemblies, looking for classes that implement <see cref="IPersistableType"/>
        /// and then calling each matching type's <c>static PersistableInitialize()</c>  method to ensure that
        /// the class' type filter is registered with <b>Linq2Couchbase</b>.
        /// </para>
        /// <note>
        /// This method scans the assemblies only the first time the method is called.  Subsequent calls will
        /// jsut return without doing anything.
        /// </note>
        /// </summary>
        public static void PersistableInitialize()
        {
            if (persistablesInitialzed)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetUserAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsClass && type.Implements<IPersistableType>())
                    {
                        var method = type.GetMethod("PersistableInitialize", new Type[] { });

                        if (method != null)
                        {
                            method.Invoke(null, new object[] { });
                        }
                    }
                }
            }

            persistablesInitialzed = true;
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

        /// <summary>
        /// Generates an database key for a persisted entity.
        /// </summary>
        /// <param name="persistedType">The entity type string.</param>
        /// <param name="args">Arguments identifying the entity.</param>
        public static string GetPersistedKey(string persistedType, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(persistedType));

            if (args.Length == 0)
            {
                throw new ArgumentException("At least one argument is expected.");
            }

            var key = $"{persistedType}::";

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (i > 0)
                {
                    key += ":";
                }

                key += arg != null ? arg.ToString() : "NULL";
            }

            return key;
        }

        /// <summary>
        /// Returns a deep clone of a <see cref="JObject"/>.
        /// </summary>
        /// <param name="jObject">The <see cref="JObject"/> or <c>null</c>.</param>
        /// <returns>The cloned instance.</returns>
        public static JObject DeepClone(JObject jObject)
        {
            if (jObject == null)
            {
                return null;
            }

            return NeonHelper.JsonClone<JObject>(jObject);
        }
    }
}
