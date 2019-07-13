//-----------------------------------------------------------------------------
// FILE:	    IDataConverter.cs
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
using System.Runtime.Serialization;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used by <see cref="Worker"/> instances to manage serialization of method parameters and results
    /// for workflow and activity methods to/from byte arrays for persistence in the Cadence cluster
    /// database.
    /// </summary>
    public interface IDataConverter
    {
        /// <summary>
        /// Deserializes a single value from a byte array.
        /// </summary>
        /// <typeparam name="T">Specifies the expected type.</typeparam>
        /// <param name="content">The input bytes.</param>
        /// <returns>The deserialized value.</returns>
        T FromData<T>(byte[] content);

        /// <summary>
        /// Deserializes an array of values from a byte array.
        /// </summary>
        /// <param name="content">The input bytes.</param>
        /// <param name="valueTypes">Specifies the expected number of array elements and their types.</param>
        /// <returns>The deserializes values.</returns>
        object[] FromDataArray(byte[] content, params Type[] valueTypes);

        /// <summary>
        /// Serializes a single value into a byte array.
        /// </summary>
        /// <param name="value">The value being serialized.</param>
        /// <returns>The serialized bytes.</returns>
        byte[] ToData(object value);
    }
}
