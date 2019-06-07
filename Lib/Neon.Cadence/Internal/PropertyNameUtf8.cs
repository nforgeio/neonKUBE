//-----------------------------------------------------------------------------
// FILE:	    PropertyNameUtf8.cs
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
using System.ComponentModel;
using System.Text;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;
using System.Diagnostics.Contracts;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Maps a property name string to its UTF-8 form.
    /// </summary>
    internal struct PropertyNameUtf8
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The property name string.</param>
        public PropertyNameUtf8(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.Name     = name;
            this.NameUtf8 = Encoding.UTF8.GetBytes(name);
        }

        /// <summary>
        /// Returns the property name as a string.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the property name encoded as IUTF-8 bytes.
        /// </summary>
        public byte[] NameUtf8 { get; private set; }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return Name.Equals(obj);
        }
    }
}
