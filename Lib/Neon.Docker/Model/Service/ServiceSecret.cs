//-----------------------------------------------------------------------------
// FILE:	    ServiceSecret.cs
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
    /// Service secret.
    /// </summary>
    public class ServiceSecret : INormalizable
    {
        /// <summary>
        /// The Docker secret ID.
        /// </summary>
        [JsonProperty(PropertyName = "SecretID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "SecretID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecretID { get; set; }

        /// <summary>
        /// The secret name.
        /// </summary>
        [JsonProperty(PropertyName = "SecretName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "SecretName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecretName { get; set; }

        /// <summary>
        /// Secret file information.
        /// </summary>
        [JsonProperty(PropertyName = "File", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Populate)]
        [YamlMember(Alias = "File", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ServiceFile File { get; set; }

        /// <inheritdoc/>
        public void Normalize()
        {
            File = File ?? new ServiceFile();

            File?.Normalize();
        }
    }
}
