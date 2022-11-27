//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyServer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using DNS.Protocol;
using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Oauth2Proxy header model.
    /// </summary>
    public class Oauth2ProxyServer
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyServer()
        {
        }

        /// <summary>
        /// The address on which to serve traffic.
        /// Leave blank or set to "-" to disable.
        /// </summary>
        [JsonProperty(PropertyName = "BindAddress", Required = Required.Always)]
        [YamlMember(Alias = "BindAddress", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string BindAddress { get; set; }

        /// <summary>
        /// The address on which to serve secure traffic.
        /// Leave blank or set to "-" to disable.
        /// </summary>
        [JsonProperty(PropertyName = "SecureBindAddress", Required = Required.Always)]
        [YamlMember(Alias = "SecureBindAddress", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecureBindAddress { get; set; }

        /// <summary>
        /// The address on which to serve secure traffic.
        /// Leave blank or set to "-" to disable.
        /// </summary>
        [JsonProperty(PropertyName = "TLS", Required = Required.Always)]
        [YamlMember(Alias = "TLS", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyTls TLS { get; set; }
    }
}