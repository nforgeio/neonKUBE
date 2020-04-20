//-----------------------------------------------------------------------------
// FILE:	    InternalDescribeDomainResponse.cs
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

using Neon.Common;
using Neon.Temporal;

using Newtonsoft.Json;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Describes a Temporal namespace.
    /// </summary>
    internal class InternalDescribeDomainResponse
    {
        /// <summary>
        /// The namespace information.
        /// </summary>
        [JsonProperty(PropertyName = "domainInfo", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalDomainInfo DomainInfo { get; set; }

        /// <summary>
        /// The namespace configuration.
        /// </summary>
        [JsonProperty(PropertyName = "configuration", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalDomainConfiguration DomainConfiguration { get; set; }

        /// <summary>
        /// Indicates whether the namespace is global.
        /// </summary>
        [JsonProperty(PropertyName = "isGlobalDomain", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool IsGlobalDomain;

        // $todo(jefflill): Ignorning:
        //
        //  DomainReplicationConfiguration 
        //  FailoverVersion

        /// <summary>
        /// Converts the internal instance into a public <see cref="DomainDescription"/>.
        /// </summary>
        /// <returns>The converted <see cref="DomainDescription"/>.</returns>
        public DomainDescription ToPublic()
        {
            return new DomainDescription()
            {
                DomainInfo     = this.DomainInfo.ToPublic(),
                Configuration  = this.DomainConfiguration.ToPublic(),
                IsGlobalDomain = this.IsGlobalDomain
            };
        }
    }
}
