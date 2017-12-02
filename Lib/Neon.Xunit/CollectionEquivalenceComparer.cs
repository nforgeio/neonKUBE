//-----------------------------------------------------------------------------
// FILE:	    CollectionComparer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;

namespace Xunit
{
    /// <summary>
    /// Compares two collections for to ensure they each contain the same items
    /// but possibly arranged in a different order.
    /// </summary>
    /// <typeparam name="T">The collection item type.</typeparam>
    public class CollectionEquivalenceComparer<T> : IEqualityComparer<IEnumerable<T>>
       where T : IEquatable<T>
    {
        /// <summary>
        /// Returns true if two collections contain the same items.
        /// </summary>
        /// <param name="collection1">Collection #1.</param>
        /// <param name="collection2">Collection #2</param>
        /// <returns><c>true</c> if the collections are identical.</returns>
        public bool Equals(IEnumerable<T> collection1, IEnumerable<T> collection2)
        {
            var leftList    = new List<T>(collection1);
            var rightList   = new List<T>(collection2);

            leftList.Sort();
            rightList.Sort();

            var enumeratorX = leftList.GetEnumerator();
            var enumeratorY = rightList.GetEnumerator();

            while (true)
            {
                var hasNextX = enumeratorX.MoveNext();
                var hasNextY = enumeratorY.MoveNext();

                if (!hasNextX || !hasNextY)
                {
                    return (hasNextX == hasNextY);
                }

                if (!enumeratorX.Current.Equals(enumeratorY.Current))
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Not implemented.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int GetHashCode(IEnumerable<T> obj)
        {
            throw new NotImplementedException();
        }
    }
}
