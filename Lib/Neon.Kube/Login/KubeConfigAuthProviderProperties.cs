//-----------------------------------------------------------------------------
// FILE:	    KubeConfigAuthProviderProperties.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Login
{
    /// <summary>
    /// Describes a Kubernetes user's credentials.
    /// </summary>
    public class KubeConfigAuthProviderProperties
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigAuthProviderProperties()
        {
        }

        /// <summary>
        /// The client id.
        /// </summary>
        [JsonProperty(PropertyName = "client-id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-id", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientId { get; set; }

        /// <summary>
        /// The client id.
        /// </summary>
        [JsonProperty(PropertyName = "client-secret", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-secret", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientSecret { get; set; }

        /// <summary>
        /// The ID Token.
        /// </summary>
        [JsonProperty(PropertyName = "id-token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id-token", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string IdToken { get; set; }

        /// <summary>
        /// The cert authority for the identity provider.
        /// </summary>
        [JsonProperty(PropertyName = "idp-certificate-authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "idp-certificate-authority", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string IdpCertificateAuthority { get; set; }

        /// <summary>
        /// The idp issuer url.
        /// </summary>
        [JsonProperty(PropertyName = "idp-issuer-url", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "idp-issuer-url", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string IdpIssuerUrl { get; set; }

        /// <summary>
        /// The client id.
        /// </summary>
        [JsonProperty(PropertyName = "refresh-token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "refresh-token", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string RefreshToken { get; set; }
    }
}
