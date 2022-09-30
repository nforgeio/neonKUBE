//-----------------------------------------------------------------------------
// FILE:	    DexConfig.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Dex configuration model.
    /// </summary>
    public class DexConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexConfig()
        {
        }

        /// <summary>
        /// The base path of dex and the external name of the OpenID Connect service.
        /// This is the canonical URL that all clients MUST use to refer to dex. If a
        /// path is provided, dex's HTTP service will listen at a non-root URL.
        /// </summary>
        [JsonProperty(PropertyName = "Issuer", Required = Required.Always)]
        [YamlMember(Alias = "issuer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Issuer { get; set; }

        /// <summary>
        /// The storage configuration determines where dex stores its state. Supported
        /// options include SQL flavors and Kubernetes third party resources. 
        /// See the documentation (https://dexidp.io/docs/storage/) for further information.
        /// </summary>
        [JsonProperty(PropertyName = "Storage", Required = Required.Always)]
        [YamlMember(Alias = "storage", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexStorage Storage { get; set; }

        /// <summary>
        /// Configuration for the http server.
        /// </summary>
        [JsonProperty(PropertyName = "Web", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "web", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexWebConfig Web { get; set; }

        /// <summary>
        /// The storage configuration determines where dex stores its state. Supported
        /// options include SQL flavors and Kubernetes third party resources. 
        /// See the documentation (https://dexidp.io/docs/storage/) for further information.
        /// </summary>
        [JsonProperty(PropertyName = "Connectors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "connectors", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<IDexConnector> Connectors { get; set; }

        /// <summary>
        /// Configuration for telemetry.
        /// </summary>
        [JsonProperty(PropertyName = "Telemetry", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "telemetry", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexTelemetryConfig Telemetry { get; set; }

        /// <summary>
        /// This block to enable the gRPC API. This values MUST be different
        /// from the HTTP endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "Grpc", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "grpc", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexGrpcConfig Grpc { get; set; }

        /// <summary>
        /// Options for controlling the logger.
        /// </summary>
        [JsonProperty(PropertyName = "Logger", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logger", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public LogOptions Logger { get; set; }

        /// <summary>
        /// Options for Oauth2 related settings.
        /// </summary>
        [JsonProperty(PropertyName = "Oauth2", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "oauth2", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexOauth2Config Oauth2 { get; set; }

        /// <summary>
        /// Let dex keep a list of passwords which can be used to login to dex.
        /// </summary>
        [JsonProperty(PropertyName = "EnablePasswordDb", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "enablePasswordDb", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? EnablePasswordDb { get; set; }

        /// <summary>
        /// A static list of passwords to login the end user. By identifying here, dex
        /// won't look in its underlying storage for passwords.
        /// <note>If this option isn't chosen users may be added through the gRPC API.</note>
        /// </summary>
        [JsonProperty(PropertyName = "StaticPasswords", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "staticPasswords", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<DexStaticUser> StaticPasswords { get; set; }
    }
}