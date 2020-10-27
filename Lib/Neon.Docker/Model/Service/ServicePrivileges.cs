//-----------------------------------------------------------------------------
// FILE:	    ServicePrivileges.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// Security options for service containers.
    /// </summary>
    public class ServicePrivileges : INormalizable
    {
        /// <summary>
        /// <b>Windows Only:</b> Windows container credential specification.
        /// </summary>
        [JsonProperty(PropertyName = "CredentialSpec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "CredentialSpec", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceCredentialSpec CredentialSpec { get; set; }

        /// <summary>
        /// SELinux labels for the container.
        /// </summary>
        [JsonProperty(PropertyName = "SELinuxContext", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "SELinuxContext", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceSELinuxContext SELinuxContext { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            // The presence or absence of these properties is significant so
            // we're not going to normalize them.
        }
    }
}
