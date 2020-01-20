//-----------------------------------------------------------------------------
// FILE:	    ServiceDnsConfig.cs
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Specifies service container DNS related options.
    /// </summary>
    public class ServiceDnsConfig : INormalizable
    {
        /// <summary>
        /// IP addresses of the nameservers.
        /// </summary>
        [JsonProperty(PropertyName = "Nameservers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Nameservers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Nameservers { get; set; }

        /// <summary>
        /// Domain search list for hostname lookups.
        /// </summary>
        [JsonProperty(PropertyName = "Search", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Search", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Search { get; set; }

        /// <summary>
        /// Low-level internal resolver options.  See: http://manpages.ubuntu.com/manpages/precise/man5/resolvconf.conf.5.html
        /// </summary>
        [JsonProperty(PropertyName = "Options", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Options", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Options { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            Nameservers = Nameservers ?? new List<string>();
            Search      = Search ?? new List<string>();
            Options     = Options ?? new List<string>();
        }
    }
}
