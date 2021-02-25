//-----------------------------------------------------------------------------
// FILE:	    XenTemplate.cs
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

using Neon.Common;

namespace Neon.XenServer
{
    /// <summary>
    /// Describes a XenServer virtual machine template.
    /// </summary>
    public class XenTemplate : XenObject
    {
        /// <summary>
        /// Constructs an instance from raw property values returned by the <b>xe CLI</b>.
        /// </summary>
        /// <param name="rawProperties">The raw object properties.</param>
        internal XenTemplate(IDictionary<string, string> rawProperties)
            : base(rawProperties)
        {
            this.Uuid            = rawProperties["uuid"];
            this.NameLabel       = rawProperties["name-label"];
            this.NameDescription = rawProperties["name-description"];
        }

        /// <summary>
        /// Returns the repository unique ID.
        /// </summary>
        public string Uuid { get; private set; }

        /// <summary>
        /// Returns the repository name.
        /// </summary>
        public string NameLabel { get; private set; }

        /// <summary>
        /// Returns the repository description.
        /// </summary>
        public string NameDescription { get; private set; }
    }
}
