//-----------------------------------------------------------------------------
// FILE:	    Oauth2ProxyOidcOptions.cs
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
    /// Oauth2Proxy OIDC option model.
    /// </summary>
    public class Oauth2ProxyOidcOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyOidcOptions()
        {
        }

        /// <summary>
        /// The OpenID Connect issuer URL.
        /// </summary>
        [JsonProperty(PropertyName = "IssuerUrl", Required = Required.Always)]
        [YamlMember(Alias = "issuerURL", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IssuerUrl { get; set; }

        /// <summary>
        /// Prevents failures if an email address in an id_token is not verified.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureAllowUnverifiedEmail", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureAllowUnverifiedEmail", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureAllowUnverifiedEmail { get; set; } = false;

        /// <summary>
        /// Skips verification of ID token issuers. When false, ID Token Issuers must match the OIDC discovery URL.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipIssuerVerification", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureSkipIssuerVerification", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipIssuerVerification { get; set; } = false;

        /// <summary>
        /// Skips verifying the ID Token's nonce claim that must match
        /// the random nonce sent in the initial OAuth flow.Otherwise, the nonce is checked
        /// after the initial OAuth redeem & subsequent token refreshes.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipNonce", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureSkipNonce", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipNonce { get; set; } = false;

        /// <summary>
        /// Allows to skip OIDC discovery and use manually supplied Endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "SkipDiscovery", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "skipDiscovery", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SkipDiscovery { get; set; } = false;

        /// <summary>
        /// JwksURL is the OpenID Connect JWKS URL
        /// </summary>
        [JsonProperty(PropertyName = "JwksUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "jwksURL", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string JwksUrl { get; set; }

        /// <summary>
        /// Indicates which claim contains the user email.
        /// </summary>
        [JsonProperty(PropertyName = "EmailClaim", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "emailClaim", ApplyNamingConventions = false)]
        [DefaultValue("email")]
        public string EmailClaim { get; set; } = "email";

        /// <summary>
        /// Indicates which claim contains the user groups.
        /// </summary>
        [JsonProperty(PropertyName = "GroupsClaim", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "groupsClaim", ApplyNamingConventions = false)]
        [DefaultValue("groups")]
        public string GroupsClaim { get; set; } = "groups";

        /// <summary>
        /// Indicates which claim contains the user ID.
        /// </summary>
        [JsonProperty(PropertyName = "UserIdClaim", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userIDClaim", ApplyNamingConventions = false)]
        [DefaultValue("email")]
        public string UserIdClaim { get; set; } = "email";

        /// <summary>
        /// Allows to define any claim that is verified against the client id.
        /// By default aud claim is used for verification.
        /// </summary>
        [JsonProperty(PropertyName = "AudienceClaims", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "audienceClaims", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> AudienceClaims { get; set; }

        /// <summary>
        /// A list of additional audiences that are allowed to pass verification in addition to the client id.
        /// </summary>
        [JsonProperty(PropertyName = "ExtraAudiences", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "extraAudiences", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> ExtraAudiences { get; set; }
    }
}