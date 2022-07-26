//-----------------------------------------------------------------------------
// FILE:	    CacheConfig.cs
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
    /// Represents the Cache configuration.
    /// </summary>
    public class Cache
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Cache()
        {

        }

        /// <summary>
        /// The type of backend cache to use. See <see cref="CacheType"/> for options.
        /// </summary>
        [JsonProperty(PropertyName = "Backend", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "backend", ApplyNamingConventions = false)]
        [DefaultValue(CacheType.InMemory)]
        public CacheType Backend { get; set; } = CacheType.InMemory;

        /// <summary>
        /// The time period to store disconnected circuit information in the cache. This should be the same as the amount of time that the upstream
        /// Blazor server keeps the circuits around. Defaults to 300 (5 minutes).
        /// </summary>
        [JsonProperty(PropertyName = "DurationSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "durationSeconds", ApplyNamingConventions = false)]
        [DefaultValue(300)]
        public int DurationSeconds { get; set; } = 300;

        /// <summary>
        /// Memcached cache config.
        /// </summary>
        [JsonProperty(PropertyName = "Memcached", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "memcached", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public MemcachedConfig Memcached { get; set; } = new MemcachedConfig();

        /// <summary>
        /// Redis cache config.
        /// </summary>
        [JsonProperty(PropertyName = "Redis", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "redis", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public RedisConfig Redis { get; set; } = new RedisConfig();
    }
}