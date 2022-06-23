//-----------------------------------------------------------------------------
// FILE:	    RedisConfig.cs
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

using Microsoft.Extensions.Caching.StackExchangeRedis;

using Newtonsoft.Json;

using StackExchange.Redis;

using YamlDotNet.Serialization;

namespace NeonBlazorProxy
{
    public class RedisConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public RedisConfig()
        {

        }

        /// <summary>
        /// The Redis Host.
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Host { get; set; } = null;

        /// <summary>
        /// The Redis port.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(6379)]
        public int Port { get; set; } = 6379;

        /// <summary>
        /// The Redis instance name.
        /// </summary>
        [JsonProperty(PropertyName = "InstanceName", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "instanceName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string InstanceName { get; set; } = null;

        /// <summary>
        /// Indicates whether Admin operations should be allowed. Defaults to true.
        /// </summary>
        [JsonProperty(PropertyName = "AllowAdmin", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "allowAdmin", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool AllowAdmin { get; set; } = true;

        /// <summary>
        /// The service name used to resolve a service via Sentinel.
        /// </summary>
        [JsonProperty(PropertyName = "ServiceName", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "serviceName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ServiceName { get; set; } = null;

        /// <summary>
        /// Returns the options as <see cref="RedisCacheOptions"/>.
        /// </summary>
        /// <returns></returns>
        public RedisCacheOptions GetOptions()
        {
            var options = new RedisCacheOptions()
            {
                Configuration = Host,
                InstanceName = InstanceName,
                ConfigurationOptions = new ConfigurationOptions()
                {
                    AllowAdmin = AllowAdmin,
                    ServiceName = ServiceName
                }
            };

            options.ConfigurationOptions.EndPoints.Add($"{Host}:{Port}");

            return options;
        }
    }
}
