//-----------------------------------------------------------------------------
// FILE:	    JsonConverter.cs
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
    /// This implementation supports values that implement <see cref="IRoundtripData"/> to make
    /// it easier to manage data schema changes. 
    /// </note>
    /// </summary>
    public class JsonConverter : IDataConverter
    {
        /// <inheritdoc/>
        public T FromData<T>(byte[] content)
        {
            Contract.Requires<ArgumentNullException>(content != null);
            Contract.Requires<ArgumentNullException>(content.Length > 0);

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
        public object[] FromDataArray(byte[] content, params Type[] valueTypes)
        {
            Contract.Requires<ArgumentNullException>(content != null);
            Contract.Requires<ArgumentNullException>(content.Length > 0);
            Contract.Requires<ArgumentNullException>(valueTypes != null);

            var jToken = JToken.Parse(Encoding.UTF8.GetString(content));

            if (jToken.Type != JTokenType.Array)
            {
                throw new ArgumentException($"Content encodes a [{jToken.Type}] instead of the expected [{JTokenType.Array}].");
            }

            var jArray = (JArray)jToken;

            if (jArray.Count != valueTypes.Length)
            {
                throw new ArgumentException($"Content array length [{jArray.Count}] does not match the expected number of values [{valueTypes.Length}].");
            }

            var output = new object[valueTypes.Length];

            for (int i = 0; i < valueTypes.Length; i++)
            {
                var type = valueTypes[i];

                if (type.Implements<IRoundtripData>())
                {
                    output[i] = RoundtripDataFactory.CreateFrom(type, content);
                }
                else
                {
                    output[i] = NeonHelper.JsonDeserialize(type, content, strict: false);
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
