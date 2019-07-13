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
                return 
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

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public byte[] ToData(object value)
        {
            return NeonHelper.JsonSerializeToBytes(value);
        }
    }
}
