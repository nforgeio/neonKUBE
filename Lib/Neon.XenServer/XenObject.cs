//-----------------------------------------------------------------------------
// FILE:	    XenObject.cs
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
using System.Collections.ObjectModel;

using Neon.Common;
using Neon.Kube;

namespace Neon.XenServer
{
    /// <summary>
    /// Base class for all XenServer objects that implement common properties.
    /// </summary>
    public class XenObject
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties">The raw object properties.</param>
        internal XenObject(IDictionary<string, string> rawProperties)
        {
            var properties = new Dictionary<string, string>();

            foreach (var item in rawProperties)
            {
                properties.Add(item.Key, item.Value);
            }

            this.Properties = new ReadOnlyDictionary<string, string>(properties);
        }

        /// <summary>
        /// Returns the read-only dictionary including all raw object properties.
        /// </summary>
        public IDictionary<string, string> Properties { get; private set; }
    }
}
