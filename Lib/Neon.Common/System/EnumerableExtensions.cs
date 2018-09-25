//-----------------------------------------------------------------------------
// FILE:	    EnumerableExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace System.Collections.Generic
{
    /// <summary>
    /// <see cref="IEnumerable"/> extension methods.
    /// </summary>
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Determines whether an <see cref="IEnumerable"/> is empty.
        /// </summary>
        /// <typeparam name="T">The enumeration value type.</typeparam>
        /// <param name="items">The items to be tested.</param>
        /// <returns><c>true</c> if <paramref name="items"/> is empty.</returns>
        public static bool IsEmpty<T>(this IEnumerable<T> items)
        {
            foreach (var item in items)
            {
                return false;
            }

            return true;
        }
    }
}
