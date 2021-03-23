//-----------------------------------------------------------------------------
// FILE:	    ObjectDictionary.cs
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
    /// Describes dictionaries mapping case-sensitive strings to objects along with nice 
    /// generic methods that converts item values to specific types.
    /// </summary>
    public class ObjectDictionary : Dictionary<string, object>, IObjectDictionary
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ObjectDictionary()
            : base()
        {
        }

        /// <inheritdoc/>
        public TValue Get<TValue>(string key)
        {
            return (TValue)base[key];
        }

        /// <inheritdoc/>
        public TValue Get<TValue>(string key, TValue @default = default(TValue))
        {
            if (base.TryGetValue(key, out var value))
            {
                return (TValue)value;
            }

            return @default;
        }

        /// <inheritdoc/>
        public bool TryGetValue<TValue>(string key, out TValue value)
        {
            if (base.TryGetValue(key, out var v))
            {
                value = (TValue)v;
                return true;
            }

            value = default(TValue);
            return false;
        }
    }
}
