//-----------------------------------------------------------------------------
// FILE:	    JsonDataConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// <para>
    /// Implements <see cref="IDataConverter"/> by serializing data to/from
    /// UTF-8 encoded JSON text.
    /// </para>
    /// <note>
    /// This converter uses the Newtonsoft <a href="https://www.newtonsoft.com/json">JSON.NET</a>
    /// package so you can decorate your data types with attributes such as <c>[JsonProperty]</c>,
    /// <c>[JsonIgnore]</c>,... to control how your data is serialized.
    /// </note>
    /// <note>
    /// This implementation also supports values that implement <see cref="IRoundtripData"/> to make
    /// it easier to manage data schema changes. 
    /// </note>
    /// </summary>
    public class JsonDataConverter : IDataConverter
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly byte[]      newlineBytes = new byte[] { 0x0A };
        private static readonly char[]      newlineArray = new char[] { '\n' };

        /// <summary>
        /// Returns a global <see cref="JsonDataConverter"/> instance.  This is used
        /// internally by <b>Neon.Temporal</b> library.
        /// </summary>
        public static JsonDataConverter Instance { get; } = new JsonDataConverter();

        //---------------------------------------------------------------------
        // Instance members

        /// <inheritdoc/>
        public T FromData<T>(byte[] content)
        {
            if (content == null)
            {
                return default(T);
            }

            Covenant.Requires<ArgumentNullException>(content.Length > 0, nameof(content));

            var type = typeof(T);

            if (type.Implements<IRoundtripData>())
            {
                return (T)RoundtripDataFactory.CreateFrom(type, content);
            }
            else
            {
                return NeonHelper.JsonDeserialize<T>(content, strict: false);
            }
        }

        /// <inheritdoc/>
        public object FromData(Type type, byte[] content)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            if (content == null)
            {
                return null;
            }

            Covenant.Requires<ArgumentNullException>(content.Length > 0, nameof(content));

            if (type.Implements<IRoundtripData>())
            {
                return RoundtripDataFactory.CreateFrom(type, content);
            }
            else
            {
                return NeonHelper.JsonDeserialize(type, content, strict: false);
            }
        }

        /// <inheritdoc/>
        public object[] FromDataArray(byte[] content, params Type[] valueTypes)
        {
            Covenant.Requires<ArgumentNullException>(valueTypes != null, nameof(valueTypes));

            if (valueTypes.Length == 0)
            {
                return Array.Empty<object>();
            }

            Covenant.Requires<ArgumentException>(content.Length > 0, nameof(content));

            var jsonText  = Encoding.UTF8.GetString(content);
            var jsonLines = jsonText.Split(newlineArray, StringSplitOptions.RemoveEmptyEntries);

            if (jsonLines.Length != valueTypes.Length)
            {
                throw new ArgumentException($"Number of arguments [{jsonLines.Length}] passed does not match the method parameter count [{valueTypes.Length}].");
            }

            var output = new object[valueTypes.Length];

            for (int i = 0; i < valueTypes.Length; i++)
            {
                var type   = valueTypes[i];
                var line   = jsonLines[i];
                var jToken = JToken.Parse(line);

                if (type.Implements<IRoundtripData>())
                {
                    switch (jToken.Type)
                    {
                        case JTokenType.Null:

                            output[i] = null;
                            break;

                        case JTokenType.Object:

                            output[i] = RoundtripDataFactory.CreateFrom(type, (JObject)jToken);
                            break;

                        default:

                            Covenant.Assert(false, $"Unexpected JSON token [{jToken}].");
                            break;
                    }
                }
                else
                {
                    output[i] = NeonHelper.JsonDeserialize(type, line);
                }
            }

            return output;
        }

        /// <inheritdoc/>
        public byte[] ToData(object value)
        {
            var roundtripData = value as IRoundtripData;

            if (roundtripData != null)
            {
                return roundtripData.ToBytes();
            }
            else
            {
                return NeonHelper.JsonSerializeToBytes(value);
            }
        }

        /// <inheritdoc/>
        public byte[] ToDataArray(object[] values)
        {
            if (values == null || values.Length == 0)
            {
                return NeonHelper.JsonSerializeToBytes(null);
            }
            else
            {
                // The GOLANG and Java client JSON data converters serialize
                // arguments with each element appearing on separate lines terminated
                // with a NEWLINE (0x0A).  We're going to do the same to be compatible.

                using (var output = new MemoryStream())
                {
                    foreach (var value in values)
                    {
                        output.Write(ToData(value));
                        output.Write(newlineBytes);
                    }

                    return output.ToArray();
                }
            }
        }
    }
}
