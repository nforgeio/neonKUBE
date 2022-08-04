//-----------------------------------------------------------------------------
// FILE:	    Backend.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using System.ComponentModel;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace NeonBlazorProxy
{
    /// <summary>
    /// <para>
    /// Defined a Blazor backend host.
    /// </para>
    /// <note>
    /// This should be a DNS entry with SRV records that point to backend servers.
    /// </note>
    /// </summary>
    public class Backend
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Backend()
        {

        }

        /// <summary>
        /// The backend host name.
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Host { get; set; } = null;

        /// <summary>
        /// The backend host port. Defaults to 80.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(80)]
        public int Port { get; set; } = 80;

        /// <summary>
        /// The scheme to use when connecting to the backend. Defaults to http.
        /// </summary>
        [JsonProperty(PropertyName = "Scheme", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "scheme", ApplyNamingConventions = false)]
        [DefaultValue("http")]
        public string Scheme { get; set; } = "http";
    }
}
