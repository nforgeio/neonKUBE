//-----------------------------------------------------------------------------
// FILE:	    InternalDomainInfo.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Cadence domain.
    /// </summary>
    internal class InternalDomainInfo
    {
        /// <summary>
        /// The domain name.
        /// </summary>
        [JsonProperty(PropertyName = "name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The domain status.
        /// </summary>
        [JsonProperty(PropertyName = "status", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DomainStatus.Registered)]
        public DomainStatus DomainStatus { get; set; }

        /// <summary>
        /// The domain description.
        /// </summary>
        [JsonProperty(PropertyName = "description", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Description { get; set; }

        /// <summary>
        /// The email address for the domain owner.
        /// </summary>
        [JsonProperty(PropertyName = "ownerEmail", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string OwnerEmail { get; set; }

        /// <summary>
        /// A dictionary of named byte data that can be attached to domain
        /// and that can be used for any purpose.
        /// </summary>
        [JsonProperty(PropertyName = "data", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, byte[]> Data;

        /// <summary>
        /// The domain's globally unique ID.
        /// </summary>
        [JsonProperty(PropertyName = "uuid", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uuid { get; set; }
    }
}
