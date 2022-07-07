//-----------------------------------------------------------------------------
// FILE:	    DnsConfig.cs
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
    /// DNS configuration options.
    /// </summary>
    public class DnsConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DnsConfig()
        {

        }

        /// <summary>
        /// Gets or sets a flag indicating whether DNS queries should use response caching
        /// or not. The cache duration is calculated by the resource record of the response.
        /// Usually, the lowest TTL is used. Default is True.
        /// </summary>
        [JsonProperty(PropertyName = "UseCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "useCache", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool UseCache { get; set; } = true;

        /// <summary>
        /// Value to override the TTL of a resource record in case the TTL of the record is higher than this maximum value. Default is 10 seconds.
        /// </summary>
        [JsonProperty(PropertyName = "MaximumCacheTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "maximumCacheTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(10)]
        public int MaximumCacheTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Value to override the TTL of a resource record in case the TTL of the record is lower than this minimum value. Default is 5 seconds.
        /// </summary>
        [JsonProperty(PropertyName = "MinimumCacheTimeoutSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "minimumCacheTimeoutSeconds", ApplyNamingConventions = false)]
        [DefaultValue(5)]
        public int MinimumCacheTimeoutSeconds { get; set; } = 5;

        /// <summary>
        /// Gets or sets a flag indicating whether the DNS failures are being cached. The purpose of caching failures is to reduce repeated lookup 
        /// attempts within a short space of time. Defaults to False.
        /// </summary>
        [JsonProperty(PropertyName = "CacheFailedResults", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cacheFailedResults", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool CacheFailedResults { get; set; } = false;
    }
}
