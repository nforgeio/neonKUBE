//-----------------------------------------------------------------------------
// FILE:	    JsonExtensions.cs
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
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Newtonsoft.Json.Linq
{
    /// <summary>
    /// Newtonsoft JSON Linq extensions.
    /// </summary>
    public static class JsonExtensions
    {
        /// <summary>
        /// Attempts to return the value of a specified <see cref="JObject"/> property
        /// converted to a specific type.
        /// </summary>
        /// <typeparam name="T">The desired type.</typeparam>
        /// <param name="jObject">The <see cref="JObject"/> instance.</param>
        /// <param name="propertyName">The property name.</param>
        /// <param name="value">Returns as the property value if present.</param>
        /// <returns><c>true</c> if the property was present and returned.</returns>
        public static bool TryGetValue<T>(this JObject jObject, string propertyName, out T value)
        {
            Covenant.Requires<ArgumentNullException>(jObject != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName));

            if (!jObject.TryGetValue(propertyName, out var jToken))
            {
                value = default(T);
                return false;
            }

            value = jObject.Value<T>(propertyName);
            return true;
        }
    }
}
