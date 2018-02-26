//-----------------------------------------------------------------------------
// FILE:	    JsonExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
