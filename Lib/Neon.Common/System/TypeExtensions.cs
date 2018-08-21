//-----------------------------------------------------------------------------
// FILE:	    TypeExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
