//-----------------------------------------------------------------------------
// FILE:	    CouchbaseIndex.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Dynamic;
using System.Linq;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.N1QL;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Retry;

namespace Couchbase
{
    /// <summary>
    /// Describes the current state of a Couchbase index.
    /// </summary>
    public class CouchbaseIndex
    {
        /// <summary>
        /// Constructs an instance from a <c>dynamic</c> object returned by
        /// a <c>select * from system:indexes</c> query.
        /// </summary>
        /// <param name="indexInfo">The index information.</param>
        internal CouchbaseIndex(dynamic indexInfo)
        {
            Covenant.Requires<ArgumentNullException>(indexInfo != null, nameof(indexInfo));
            Covenant.Requires<ArgumentException>(indexInfo.indexes != null, nameof(indexInfo));

            var index = indexInfo.indexes;

            this.Name      = (string)index.name;
            this.Type      = (string)index.@using ?? "gsi";
            this.IsPrimary = (bool?)index.is_primary ?? false;
            this.State     = (string)index.state ?? string.Empty;
            this.Where     = (string)index.condition;

            var keyArray = (JArray)index.index_key;

            if (keyArray == null)
            {
                Keys = new string[0];
            }
            else
            {
                Keys = new string[keyArray.Count];

                for (int i = 0; i < keyArray.Count; i++)
                {
                    Keys[i] = (string)keyArray[i];
                }
            }
        }

        /// <summary>
        /// Returns the index name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Identifies the technology used to host the index, currently one of
        /// <b>gsi</b> or <b>view</b>.  This corresponds to the underlying
        /// Couchbase <b>using</b> property.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// Returns <c>true</c> for primary indexes.
        /// </summary>
        public bool IsPrimary { get; private set; }

        /// <summary>
        /// Returns the index state.
        /// </summary>
        public string State { get; private set; }

        /// <summary>
        /// Returns the array of index keys.
        /// </summary>
        public string[] Keys { get; private set; }

        /// <summary>
        /// Returns the index's <b>where</b> condition.
        /// </summary>
        public string Where { get; private set; }
    }
}