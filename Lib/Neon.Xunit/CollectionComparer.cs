//-----------------------------------------------------------------------------
// FILE:	    CollectionComparer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Xunit
{
    /// <summary>
    /// Compares two collections for strict equality by ensuring they
    /// have the same items in the same order.
    /// </summary>
    /// <typeparam name="T">The collection item type.</typeparam>
    public class CollectionComparer<T> : IEqualityComparer<IEnumerable<T>>
       where T : IEquatable<T>
    {
        /// <summary>
        /// Returns true if two collections are identical.
        /// </summary>
        /// <param name="collection1">Collection #1.</param>
        /// <param name="collection2">Collection #2</param>
        /// <returns><c>true</c> if the collections are identical.</returns>
        public bool Equals(IEnumerable<T> collection1, IEnumerable<T> collection2)
        {
            var leftList    = new List<T>(collection1);
            var rightList   = new List<T>(collection2);
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
