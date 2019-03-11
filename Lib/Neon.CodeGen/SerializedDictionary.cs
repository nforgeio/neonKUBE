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
// I need to measure how expensive this is.

namespace Neon.CodeGen
{
    /// <summary>
    /// Internal dictionary implemenation used to keep the runtime
    /// dictionary state synchronized with the backing <see cref="JObject"/>.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    internal sealed class SerializedDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private JProperty                   property;
        private Dictionary<TKey, TValue>    dictionary;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="backingProperty">The backing <see cref="JProperty"/>.</param>
        public SerializedDictionary(JProperty backingProperty)
        {
            Covenant.Requires<ArgumentNullException>(backingProperty != null);

            this.property = backingProperty;
            this.dictionary = property.Value.ToObject<Dictionary<TKey, TValue>>();
        }

        /// <summary>
        /// Performs an operation on the dictionary, ensuring that
        /// the dictionary exists and updating the backing property
        /// afterwards.
        /// </summary>
        /// <param name="operation">The operation to be performed.</param>
        private void ModifyOperation(Action operation)
        {
            lock (property)
            {
                if (dictionary == null)
                {
                    dictionary = new Dictionary<TKey, TValue>();
                }

                operation();

                property.Value = JToken.FromObject(dictionary);
            }
        }

        /// <summary>
        /// Performs an operation on the dictionary that returns a
        /// result, ensuring that the dictionary exists.
        /// </summary>
        /// <typeparam name="TResult">Thr result type.</typeparam>
        /// <param name="operation">The operation to be performed.</param>
        /// <returns>The operation result.</returns>
        private TResult QueryOperation<TResult>(Func<TResult> operation)
        {
            lock (property)
            {
                if (dictionary == null)
                {
                    dictionary     = new Dictionary<TKey, TValue>();
                    property.Value = JToken.FromObject(dictionary);
                }

                return operation();
            }
        }

        /// <inheritdoc/>
        public TValue this[TKey key]
        {
            get => QueryOperation(() => dictionary[key]);

            set
            {
                ModifyOperation(() =>
                {
                    dictionary[key] = value;
                });
            }
        }

        /// <inheritdoc/>
        public ICollection<TKey> Keys => QueryOperation(() => dictionary.Keys);

        /// <inheritdoc/>
        public ICollection<TValue> Values => QueryOperation(() => dictionary.Values);

        /// <inheritdoc/>
        public int Count => QueryOperation(() => dictionary.Count);

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public void Add(TKey key, TValue value)
        {
            ModifyOperation(() => dictionary.Add(key, value));
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<TKey, TValue> item)
        {
            ModifyOperation(() => dictionary.Add(item.Key, item.Value));
        }

        /// <inheritdoc/>
        public void Clear()
        {
            ModifyOperation(() => dictionary.Clear());
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            return QueryOperation(() => dictionary.ContainsKey(item.Key));
        }

        /// <inheritdoc/>
        public bool ContainsKey(TKey key)
        {
            return QueryOperation(() => dictionary.ContainsKey(key));
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (property)
            {
                if (dictionary == null)
                {
                    dictionary     = new Dictionary<TKey, TValue>();
                    property.Value = JToken.FromObject(dictionary);
                }

                var index = 0;

                foreach (var item in dictionary)
                {
                    array[arrayIndex + index] = item;
                    index++;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return QueryOperation(() => dictionary.GetEnumerator());
        }

        /// <inheritdoc/>
        public bool Remove(TKey key)
        {
            var removed = false;

            ModifyOperation(() => removed = dictionary.Remove(key));

            return removed;
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        /// <inheritdoc/>
        public bool TryGetValue(TKey key, out TValue value)
        {
            TValue output = default(TValue);

            try
            {
                return QueryOperation(() => dictionary.TryGetValue(key, out output));
            }
            finally
            {
                value = output;
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return QueryOperation(() => ((IEnumerable)dictionary).GetEnumerator());
        }
    }
}
