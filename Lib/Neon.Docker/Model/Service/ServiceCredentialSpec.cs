//-----------------------------------------------------------------------------
// FILE:	    ServiceCredentialSpec.cs
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// <b>Windows-only:</b> Specifies how Windows credentials are to be
    /// loaded for the container.
    /// </summary>
    public class ServiceCredentialSpec : INormalizable
    {
        /// <summary>
        /// Specifies the file on the Docker host with the credentials.
        /// </summary>
        [JsonProperty(PropertyName = "File", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "File", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string File { get; set; }

        /// <summary>
        /// Specifies the Windows registry location on the Docker host with the
        /// credentials.
        /// </summary>
        [JsonProperty(PropertyName = "Registry", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "Registry", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Registry { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            // The presence or abence of these properties is important so we're
            // not going to normalize them.
        }
    }
}
