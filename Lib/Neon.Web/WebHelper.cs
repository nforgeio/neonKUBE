//-----------------------------------------------------------------------------
// FILE:	    WebHelper.cs
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
using System.Threading;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Web
{
    /// <summary>
    /// Utility methods for <b>AspNetCore</b> applications.
    /// </summary>
    public static partial class WebHelper
    {
        // Identifies the types that can be serialized and deserialized by the round-trip formatters.

        private static HashSet<Type> roundTripTypes = new HashSet<Type>();

        /// <summary>
        /// Static constructor.
        /// </summary>
        static WebHelper()
        {
            // Initialize the round-trip types hashset with the low-level .NET types
            // we'll support.  Note that the [IGeneratedTypes] will be registered
            // by the round-trip formatters dynamically.  These are the same primitive
            // types that JSON.NET supports:
            //
            //      https://www.newtonsoft.com/json/help/html/SerializationGuide.htm

            RegisterRoundTripType(typeof(string));
            RegisterRoundTripType(typeof(byte));
            RegisterRoundTripType(typeof(sbyte));
            RegisterRoundTripType(typeof(ushort));
            RegisterRoundTripType(typeof(short));
            RegisterRoundTripType(typeof(uint));
            RegisterRoundTripType(typeof(int));
            RegisterRoundTripType(typeof(ulong));
            RegisterRoundTripType(typeof(long));
            RegisterRoundTripType(typeof(float));
            RegisterRoundTripType(typeof(double));
            RegisterRoundTripType(typeof(decimal));
            RegisterRoundTripType(typeof(DateTime));
            RegisterRoundTripType(typeof(byte[]));
            RegisterRoundTripType(typeof(Guid));

            // The formatters also automatically support types
            // with extended Neon JSON type converters.

            foreach (var converter in NeonHelper.JsonStrictSerializerSettings.Value.Converters)
            {
                var enhancedConverter = converter as IEnhancedJsonConverter;

                if (enhancedConverter != null)
                {
                    RegisterRoundTripType(enhancedConverter.Type);
                }
            }
        }

        /// <summary>
        /// Registers a type as being round-trippable.
        /// </summary>
        /// <param name="type">The type.</param>
        internal static void RegisterRoundTripType(Type type)
        {
            lock (roundTripTypes)
            {
                if (!roundTripTypes.Contains(type))
                {
                    roundTripTypes.Add(type);
                }
            }
        }

        /// <summary>
        /// Determines whether a type is round-trippable.
        /// </summary>
        /// <param name="type">The type to be tested.</param>
        /// <returns><c>true</c> if the type is round-trippable.</returns>
        internal static bool IsRoundTripType(Type type)
        {
            if (type.IsEnum)
            {
                return true;    // All enum types are supported.
            }

            lock (roundTripTypes)
            {
                return roundTripTypes.Contains(type);
            }
        }

        /// <summary>
        /// Generates an opaque globally unique activity ID.
        /// </summary>
        /// <returns>The activity ID string.</returns>
        public static string GenerateActivityId()
        {
            return NeonHelper.UrlTokenEncode(Guid.NewGuid().ToByteArray());
        }
    }
}
