//-----------------------------------------------------------------------------
// FILE:	    InternalDomainInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Temporal namespace.
    /// </summary>
    internal class InternalDomainInfo
    {
        /// <summary>
        /// The namespace name.
        /// </summary>
        [JsonProperty(PropertyName = "name", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The namespace status.
        /// </summary>
        [JsonProperty(PropertyName = "status", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DomainStatus.Registered)]
        public DomainStatus DomainStatus { get; set; }

        /// <summary>
        /// The namespace description.
        /// </summary>
        [JsonProperty(PropertyName = "description", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Description { get; set; }

        /// <summary>
        /// The email address for the namespace owner.
        /// </summary>
        [JsonProperty(PropertyName = "ownerEmail", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string OwnerEmail { get; set; }

        /// <summary>
        /// A dictionary of named byte data that can be attached to namespace
        /// and that can be used for any purpose.
        /// </summary>
        [JsonProperty(PropertyName = "data", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, byte[]> Data;

        /// <summary>
        /// The namespace's globally unique ID.
        /// </summary>
        [JsonProperty(PropertyName = "uuid", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uuid { get; set; }

        /// <summary>
        /// Converts the internal instance into a public <see cref="DomainInfo"/>.
        /// </summary>
        /// <returns>The converted <see cref="DomainInfo"/>.</returns>
        public DomainInfo ToPublic()
        {
            // $todo(jefflill): DomainInfo doesn't currently include these properties:
            //
            //  Data
            //  Uuid    

            return new DomainInfo()
            {
                Description = this.Description,
                Name        = this.Name,
                OwnerEmail  = this.OwnerEmail,
                Status      = this.DomainStatus
            };
        }
    }
}
