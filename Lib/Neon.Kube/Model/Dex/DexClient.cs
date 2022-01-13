//-----------------------------------------------------------------------------
// FILE:	    DexClient.cs
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
    /// Defines a Dex client app.
    /// </summary>
    public class DexClient
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexClient()
        {
        }

        /// <summary>
        /// Client ID
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Id { get; set; }

        /// <summary>
        /// List of valid redirect URIs for the client.
        /// </summary>
        [JsonProperty(PropertyName = "RedirectUris", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "redirectUris", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> RedirectUris { get; set; }

        /// <summary>
        /// Client name.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Client secret.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Secret", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "secret", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Secret { get; set; }

        /// <summary>
        /// Whether the client is public. Public clients do not need secrets.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "Public", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "public", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool? Public { get; set; }

        /// <summary>
        /// List of trusted clients peers.
        /// </summary>
        [JsonProperty(PropertyName = "TrustedPeers", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "trustedPeers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> TrustedPeers { get; set; }
    }
}