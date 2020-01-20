//-----------------------------------------------------------------------------
// FILE:	    ServicePlatform.cs
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
    /// Service container spread placement settings.
    /// </summary>
    public class ServicePlatform : INormalizable
    {
        /// <summary>
        /// Specifies the hardware architecture (like: <b>x86_64</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Architecture", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Architecture", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Architecture { get; set; }

        /// <summary>
        /// Specifies the operating system (like: <b>linux</b> or <b>windows</b>).
        /// </summary>
        [JsonProperty(PropertyName = "OS", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "OS", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string OS { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
