//-----------------------------------------------------------------------------
// FILE:	    ServiceTmpfsOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Volume Tempfs options.
    /// </summary>
    public class ServiceTmpfsOptions : INormalizable
    {
        /// <summary>
        /// Specifies the <b>tmpfs</b> size in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "SizeBytes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "SizeBytes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public long? SizeBytes { get; set; } = 0;

        /// <summary>
        /// Specifies the <b>tmpfs</b> file permission mode encoded as an integer.
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Mode", ApplyNamingConventions = false)]
        [DefaultValue(1023)]    // 1777 Linux octal file mode converted to decimal
        public int Mode { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
        }
    }
}
