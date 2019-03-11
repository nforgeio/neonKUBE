//-----------------------------------------------------------------------------
// FILE:	    SerializedDictionary.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// $todo(jeff.lill):
//
// This is about 390 times slower than a stock List<T>, handling
// about 1.5 million operations per second.

namespace Neon.CodeGen
{
    /// <summary>
    /// Internal list implemenation used to keep the runtime list 
    /// state synchronized with the backing <see cref="JObject"/>.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    internal sealed class SerializedList<T> : IList<T>
    {
        private JProperty   property;
        private List<T>     list;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="backingProperty">The backing <see cref="JProperty"/>.</param>
        public SerializedList(JProperty backingProperty)
        {
            Covenant.Requires<ArgumentNullException>(backingProperty != null);

            this.property = backingProperty;
            this.list     = property.Value.ToObject<List<T>>();
        }

        /// <summary>
        /// Performs an operation on the dictionary, ensuring that
        /// the dictionary exists and updating the backing property
        /// afterwards.
        /// </summary>
        /// <param name="operation">The operation to be performed.</param>
        private void Modify(Action operation)
        {
            lock (property)
            {
                if (list == null)
                {
                    list = new List<T>();
                }

                operation();

                property.Value = JToken.FromObject(list);
            }
        }

        /// <summary>
        /// Performs an operation on the dictionary that returns a
        /// result, ensuring that the dictionary exists.
        /// </summary>
        /// <typeparam name="TResult">Thr result type.</typeparam>
        /// <param name="operation">The operation to be performed.</param>
        /// <returns>The operation result.</returns>
        private TResult Query<TResult>(Func<TResult> operation)
        {
            lock (property)
            {
                if (list == null)
                {
                    list           = new List<T>();
                    property.Value = JToken.FromObject(list);
                }

                return operation();
            }
        }

        /// <inheritdoc/>
        public T this[int index]
        {
            get => Query(() => list[index]);
            set => Modify(() => list[index] = value);
        }

        /// <inheritdoc/>
        public int Count => Query(() => list.Count);

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(T item)
        {
            Modify(() => list.Add(item));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            Modify(() => list.Clear());
        }

        /// <inheritdoc/>
        public bool Contains(T item)
        {
            return Query(() => list.Contains(item));
        }

        /// <inheritdoc/>
        public void CopyTo(T[] array, int arrayIndex)
        {
            Modify(() =>
            {
                var index = 0;

                foreach (var item in list)
                {
                    array[arrayIndex + index] = item;
                    index++;
                }
            });
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator()
        {
            return Query(() => list.GetEnumerator());
        }

        /// <inheritdoc/>
        public int IndexOf(T item)
        {
            return Query(() => list.IndexOf(item));
        }

        /// <inheritdoc/>
        public void Insert(int index, T item)
        {
            Modify(() => list.Insert(index, item));
        }

        /// <inheritdoc/>
        public bool Remove(T item)
        {
            return Query(() => list.Remove(item));
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            Modify(() => list.RemoveAt(index));
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return Query(() => ((IEnumerable)list).GetEnumerator());
        }
    }
}
