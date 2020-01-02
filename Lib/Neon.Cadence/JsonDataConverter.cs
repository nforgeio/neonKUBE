//-----------------------------------------------------------------------------
// FILE:	    JsonDataConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// Implements <see cref="IDataConverter"/> by serializing data to/from
    /// UTF-8 encoded JSON text.
    /// </para>
    /// <note>
    /// This implementation also supports values that implement <see cref="IRoundtripData"/> to make
    /// it easier to manage data schema changes. 
    /// </note>
    /// </summary>
    public class JsonDataConverter : IDataConverter
    {
        /// <inheritdoc/>
        public T FromData<T>(byte[] content)
        {
            Covenant.Requires<ArgumentNullException>(content != null, nameof(content));
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
            Covenant.Requires<ArgumentNullException>(content != null, nameof(content));
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
            Covenant.Requires<ArgumentNullException>(content != null, nameof(content));
            Covenant.Requires<ArgumentNullException>(content.Length > 0, nameof(content));
            Covenant.Requires<ArgumentNullException>(valueTypes != null, nameof(valueTypes));

            var jToken = JToken.Parse(Encoding.UTF8.GetString(content));

            if (jToken.Type != JTokenType.Array)
            {
                throw new ArgumentException($"Content encodes a [{jToken.Type}] instead of the expected [{JTokenType.Array}].", nameof(jToken));
            }

            var jArray = (JArray)jToken;

            if (jArray.Count != valueTypes.Length)
            {
                throw new ArgumentException($"Content array length [{jArray.Count}] does not match the expected number of values [{valueTypes.Length}].", nameof(jArray));
            }

            var output = new object[valueTypes.Length];

            for (int i = 0; i < valueTypes.Length; i++)
            {
                var type = valueTypes[i];
                var item = jArray[i];

                if (type.Implements<IRoundtripData>())
                {
                    switch (item.Type)
                    {
                        case JTokenType.Null:

                            output[i] = null;
                            break;

                        case JTokenType.Object:

                            output[i] = RoundtripDataFactory.CreateFrom(type, (JObject)item);
                            break;

                        default:

                            throw new ArgumentException($"Unexpected [{item.Type}] in JSON array.  Only [{nameof(JTokenType.Object)}] or [{nameof(JTokenType.Null)}] are allowed.", nameof(item));
                    }
                }
                else
                {
                    output[i] = item.ToObject(type);
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
    }
}
