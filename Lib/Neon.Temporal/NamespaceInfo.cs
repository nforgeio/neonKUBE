//-----------------------------------------------------------------------------
// FILE:	    NamespaceInfo.cs
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
using System.ComponentModel;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Data;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Information about a Temporal namespace.
    /// </summary>
    public class NamespaceInfo
    {
        /// <summary>
        /// The namespace name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The namespace UUID.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The namespace status.
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<NamespaceState>))]
        public NamespaceState State { get; set; }

        /// <summary>
        /// Ths namespace description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The namespace owner's email address.
        /// </summary>
        [JsonProperty(PropertyName = "owner_email")]
        public string OwnerEmail { get; set; }

        /// <summary>
        /// A dictionary of named string data that can be attached to namespace
        /// and that can be used for any purpose.
        /// </summary>
        public Dictionary<string, string> Data { get; set; }
    }
}
