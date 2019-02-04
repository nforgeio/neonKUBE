//-----------------------------------------------------------------------------
// FILE:	    TypeExtensions.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    /// <summary>
    /// Type extension methods.
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Determines whether a <see cref="System.Type"/> implements a specific interface.
        /// </summary>
        /// <typeparam name="TInterface">The required interface type.</typeparam>
        /// <param name="type">The type beinbg tested.</param>
        /// <returns><c>true</c> if <paramref name="type"/> implements <paramref name="type"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="type"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if <typeparamref name="TInterface"/> is not an <c>interface</c>.</exception>
        public static bool Implements<TInterface>(this Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null);
            Covenant.Requires<ArgumentException>(typeof(TInterface).IsInterface, $"Type [{nameof(TInterface)}] is not an interface.");

            return type.GetInterfaces().Contains(typeof(TInterface));
        }
    }
}
