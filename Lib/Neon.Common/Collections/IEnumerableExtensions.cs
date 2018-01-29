//-----------------------------------------------------------------------------
// FILE:	    IEnumerableExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace System.Collections.Generic
{
    /// <summary>
    /// <see cref="IEnumerable{T}"/> extension methods.
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Selects one or more randomly selected items from an enumeration.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="items">The source items.</param>
        /// <param name="count">The number of values to be returned (defaults to <b>1</b>.</param>
        /// <returns>The randomly selected items as an enumeration.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="count"/> is not positive or if <paramref name="items"/> does
        /// not have at least <paramref name="count"/> items.
        /// </exception>
        public static IEnumerable<T> SelectRandom<T>(this IEnumerable<T> items, int count = 1)
        {
            Covenant.Requires<ArgumentNullException>(items != null);
            Covenant.Requires<ArgumentException>(count > 0);

            var sourceValues = items.ToList();

            if (sourceValues.Count < count)
            {
                throw new ArgumentException($"Cannot select [{count}] random items from a collection that has only [{sourceValues}] items.");
            }
            else if (sourceValues.Count == count)
            {
                return sourceValues;
            }

            var outputValues = new List<T>(count);

            for (int i = 0; i < count; i++)
            {
                var index = NeonHelper.RandIndex(sourceValues.Count);

                outputValues.Add(sourceValues[index]);
                sourceValues.RemoveAt(index);
            }

            return outputValues;
        }

        /// <summary>
        /// Returns the zero-based indexed item from an enumeration or <c>default(T)</c> if
        /// there is no item at the specfied index.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="items">The enumerated items.</param>
        /// <param name="index">The zero-based index.</param>
        /// <returns>The indexed item or <c>default(T)</c>.</returns>
        public static T IndexedOrDefault<T>(this IEnumerable<T> items, int index)
        {
            Covenant.Requires<ArgumentException>(index >= 0);

            foreach (var item in items)
            {
                if (index == 0)
                {
                    return item;
                }

                index--;
            }

            return default(T);
        }
    }
}
