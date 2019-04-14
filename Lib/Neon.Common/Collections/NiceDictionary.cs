//-----------------------------------------------------------------------------
// FILE:	    NiceDictionary.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Retry;

namespace Neon.Collections
{
    /// <summary>
    /// A dictionary of values where the indexer will return the default value
    /// for keys that don't map to an item.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    public class NiceDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        /// <summary>
        /// Accesses the value associated with a specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>
        /// The associated value or the <c>default</c> value for the
        /// type when there's no associated value.
        /// </returns>
        public new TValue this[TKey key]
        {
            get
            {
                if (base.TryGetValue(key, out TValue value))
                {
                    return value;
                }
                else
                {
                    return default(TValue);
                }
            }

            set => base[key] = value;
        }
    }
}
