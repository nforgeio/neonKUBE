//-----------------------------------------------------------------------------
// FILE:	    NewtonsoftExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    public static class NewtonsoftExtensions
    {
        //---------------------------------------------------------------------
        // JObject extensions

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
            Covenant.Requires<ArgumentNullException>(jObject != null, nameof(jObject));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(propertyName), nameof(propertyName));

            if (!jObject.TryGetValue(propertyName, out var jToken))
            {
                value = default(T);
                return false;
            }

            value = jObject.Value<T>(propertyName);
            return true;
        }

        //---------------------------------------------------------------------
        // JsonSerializerSettings extensions

        /// <summary>
        /// Copies the settings from one <see cref="JsonSerializerSettings"/> instance to another.
        /// </summary>
        /// <param name="source">The source settings.</param>
        /// <param name="target">The target instance.</param>
        public static void CopyTo(this JsonSerializerSettings source, JsonSerializerSettings target)
        {
            target.CheckAdditionalContent         = source.CheckAdditionalContent;
            target.ConstructorHandling            = source.ConstructorHandling;
            target.Context                        = source.Context;
            target.ContractResolver               = source.ContractResolver;
            target.Converters                     = source.Converters;
            target.Culture                        = source.Culture;
            target.DateFormatHandling             = source.DateFormatHandling;
            target.DateFormatString               = source.DateFormatString;
            target.DateParseHandling              = source.DateParseHandling;
            target.DateTimeZoneHandling           = source.DateTimeZoneHandling;
            target.DefaultValueHandling           = source.DefaultValueHandling;
            target.EqualityComparer               = source.EqualityComparer;
            target.Error                          = source.Error;
            target.FloatFormatHandling            = source.FloatFormatHandling;
            target.FloatParseHandling             = source.FloatParseHandling;
            target.Formatting                     = source.Formatting;
            target.MaxDepth                       = source.MaxDepth;
            target.MetadataPropertyHandling       = source.MetadataPropertyHandling;
            target.MissingMemberHandling          = source.MissingMemberHandling;
            target.NullValueHandling              = source.NullValueHandling;
            target.ObjectCreationHandling         = source.ObjectCreationHandling;
            target.PreserveReferencesHandling     = source.PreserveReferencesHandling;
            target.ReferenceLoopHandling          = source.ReferenceLoopHandling;
            target.ReferenceResolverProvider      = source.ReferenceResolverProvider;
            target.SerializationBinder            = source.SerializationBinder;
            target.StringEscapeHandling           = source.StringEscapeHandling;
            target.TraceWriter                    = source.TraceWriter;
            target.TypeNameAssemblyFormatHandling = source.TypeNameAssemblyFormatHandling;
            target.TypeNameHandling               = source.TypeNameHandling;
        }
    }
}
