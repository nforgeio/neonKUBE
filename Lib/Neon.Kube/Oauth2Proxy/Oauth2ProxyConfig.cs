//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyConfig.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy configuration model.
    /// </summary>
    public class Oauth2ProxyConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyConfig()
        {
        }

        /// <summary>
        /// Used to configure headers that should be added to requests to upstream servers.
        /// Headers may source values from either the authenticated user's session
        /// or from a static secret value.
        /// </summary>
        [JsonProperty(PropertyName = "InjectRequestHeaders", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "injectRequestHeaders", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyHeader> InjectRequestHeaders { get; set; }

        /// <summary>
        /// Used to configure headers that should be added to responses from the proxy.
        /// This is typically used when using the proxy as an external authentication
        /// provider in conjunction with another proxy such as NGINX and its
        /// auth_request module.
        /// Headers may source values from either the authenticated user's session
        /// or from a static secret value.
        /// </summary>
        [JsonProperty(PropertyName = "InjectResponseHeaders", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "injectResponseHeaders", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyHeader> InjectResponseHeaders { get; set; }

        /// <summary>
        /// Used to configure headers that should be added to responses from the proxy.
        /// This is typically used when using the proxy as an external authentication
        /// provider in conjunction with another proxy such as NGINX and its
        /// auth_request module.
        /// Headers may source values from either the authenticated user's session
        /// or from a static secret value.
        /// </summary>
        [JsonProperty(PropertyName = "Server", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "server", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyServer Server { get; set; }

        /// <summary>
        /// Used to configure the HTTP(S) server for metrics.
        /// You may choose to run both HTTP and HTTPS servers simultaneously.
        /// This can be done by setting the BindAddress and the SecureBindAddress simultaneously.
        /// To use the secure server you must configure a TLS certificate and key.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsServer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "metricsServer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyServer MetricsServer { get; set; }

        /// <summary>
        /// Used to configure multiple providers.
        /// </summary>
        [JsonProperty(PropertyName = "Providers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "providers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<Oauth2ProxyProvider> Providers { get; set; }

        /// <summary>
        /// Used to configure upstream servers. Once a user is authenticated, 
        /// requests to the server will be proxied to
        /// these upstream servers based on the path mappings defined in this list.
        /// </summary>
        [JsonProperty(PropertyName = "UpstreamConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "upstreamConfig", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyUpstreamConfig UpstreamConfig { get; set; }
    }
}