//-----------------------------------------------------------------------------
// FILE:	    MemcachedConfig.cs
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

using Enyim;
using Enyim.Caching.Configuration;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace NeonBlazorProxy
{
    public class MemcachedConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public MemcachedConfig()
        {

        }

        /// <summary>
        /// The Memcached Host.
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Address { get; set; } = null;

        /// <summary>
        /// The Memcached port.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(11211)]
        public int Port { get; set; } = 11211;

        /// <summary>
        /// Returns the options as <see cref="MemcachedClientOptions"/>.
        /// </summary>
        /// <returns></returns>
        public MemcachedClientOptions GetOptions()
        {
            var options = new MemcachedClientOptions();

            options.Servers.Add(new Server()
            {
                Address = Address,
                Port = Port
            });

            return options;
        }
    }
}
