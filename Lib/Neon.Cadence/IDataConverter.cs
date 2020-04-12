//-----------------------------------------------------------------------------
// FILE:	    IDataConverter.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
        /// Deserializes a single value from a byte array as the specified generic type parameter.
        /// </summary>
        /// <typeparam name="T">Specifies the result type.</typeparam>
        /// <param name="content">The input bytes.</param>
        /// <returns>The deserialized value.</returns>
        T FromData<T>(byte[] content);

        /// <summary>
        /// Deserializes a single value from a byte array as the specified type.
        /// </summary>
        /// <param name="type">The result type.</param>
        /// <param name="content">The input bytes.</param>
        /// <returns>The deserialized value returned as an <see cref="object"/>.</returns>
        object FromData(Type type, byte[] content);

        /// <summary>
        /// Deserializes an array of values from a byte array.
        /// </summary>
        /// <param name="content">The input bytes.</param>
        /// <param name="valueTypes">Specifies the expected number of array elements and their types.</param>
        /// <returns>The deserialized values.</returns>
        object[] FromDataArray(byte[] content, params Type[] valueTypes);

        /// <summary>
        /// Serializes zero or more values into a byte array.
        /// </summary>
        /// <param name="values">The value being serialized.</param>
        /// <returns>The serialized bytes.</returns>
        /// <remarks>
        /// <para><b>Implementation Notes:</b></para>
        /// <para>
        /// To maintain compatibility with non .NET clients like GOLANG and Java you'll
        /// need to follow these conventions:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     If <paramref name="values"/> is <c>null</c> or empty then just serialize a <c>null</c>.
        ///     </item>
        ///     <item>
        ///     If <paramref name="values"/> includes a single value then just serialize that
        ///     value; don't wrap it into an array.
        ///     </item>
        ///     <item>
        ///     If <paramref name="values"/> includes more than one value then serialize them
        ///     as an array.
        ///     </item>
        /// </list>
        /// </remarks>
        byte[] ToData(params object[] values);
    }
}
