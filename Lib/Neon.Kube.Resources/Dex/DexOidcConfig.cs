//-----------------------------------------------------------------------------
// FILE:	    DexOidcConfig.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2021 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Resources.Dex
{
    /// <summary>
    /// Configuration for backend connectors.
    /// </summary>
    public class DexOidcConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexOidcConfig()
        {
        }

        /// <summary>
        /// OIDC Issuer.
        /// </summary>
        [JsonProperty(PropertyName = "Issuer", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "issuer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Issuer { get; set; }

        /// <summary>
        /// OIDC client ID.
        /// </summary>
        [JsonProperty(PropertyName = "ClientId", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clientId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClientId { get; set; }

        /// <summary>
        /// OIDC client Secret.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSecret", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clientSecret", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClientSecret { get; set; }

        /// <summary>
        /// The OIDC Redirect URL.
        /// </summary>
        [JsonProperty(PropertyName = "RedirectURI", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "redirectURI", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RedirectURI { get; set; }

        /// <summary>
        /// Causes client_secret to be passed as POST parameters instead of basic
        /// auth. This is specifically "NOT RECOMMENDED" by the OAuth2 RFC, but some
        /// providers require it.
        ///
        /// https://tools.ietf.org/html/rfc6749#section-2.3.1
        /// </summary>
        [JsonProperty(PropertyName = "BasicAuthUnsupported", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "basicAuthUnsupported", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? BasicAuthUnsupported { get; set; }

        /// <summary>
        /// OIDC Scopes.
        /// </summary>
        [JsonProperty(PropertyName = "Scopes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "scopes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Scopes { get; set; }

        /// <summary>
        /// OIDC Scopes.
        /// </summary>
        [JsonProperty(PropertyName = "RootCAs", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "rootCAs", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> RootCAs { get; set; }

        /// <summary>
        /// Override the value of email_verifed to true in the returned claims.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipEmailVerified", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureSkipEmailVerified", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipEmailVerified { get; set; } = false;

        /// <summary>
        /// InsecureEnableGroups enables groups claims. This is disabled by default until https://github.com/dexidp/dex/issues/1065 is resolved.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureEnableGroups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureEnableGroups", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureEnableGroups { get; set; } = false;

        /// <summary>
        /// AcrValues (Authentication Context Class Reference Values) that specifies the Authentication Context Class Values
        /// within the Authentication Request that the Authorization Server is being requested to use for
        /// processing requests from this Client, with the values appearing in order of preference.
        /// </summary>
        [JsonProperty(PropertyName = "AcrValues", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "acrValues", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> AcrValues { get; set; }

        /// <summary>
        /// Disable certificate verification.
        /// </summary>
        [JsonProperty(PropertyName = "InsecureSkipVerify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecureSkipVerify", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipVerify { get; set; } = false;

        /// <summary>
        /// GetUserInfo uses the userinfo endpoint to get additional claims for
        /// the token. This is especially useful where upstreams return "thin"
        /// id tokens
        /// </summary>
        [JsonProperty(PropertyName = "GetUserInfo", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "getUserInfo", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool GetUserInfo { get; set; } = false;

        /// <summary>
        /// Spoecifies the User ID key.
        /// </summary>
        [JsonProperty(PropertyName = "UserIDKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userIDKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UserIDKey { get; set; } = null;

        /// <summary>
        /// Spoecifies the User ID key.
        /// </summary>
        [JsonProperty(PropertyName = "UserNameKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "userNameKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string UserNameKey { get; set; } = null;

        /// <summary>
        /// PromptType will be used for the prompt parameter (when offline_access, by default prompt=consent).
        /// </summary>
        [JsonProperty(PropertyName = "PromptType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "promptType", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PromptType { get; set; } = null;

        /// <summary>
        /// OverrideClaimMapping will be used to override the options defined in claimMappings.
        /// i.e. if there are 'email' and `preferred_email` claims available, by default Dex will always use the `email` claim independent of the ClaimMapping.EmailKey.
        /// This setting allows you to override the default behavior of Dex and enforce the mappings defined in `claimMapping`.
        /// </summary>
        [JsonProperty(PropertyName = "OverrideClaimMapping", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "overrideClaimMapping", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool OverrideClaimMapping { get; set; } = false;

        /// <summary>
        /// The claim mapping overrides.
        /// </summary>
        [JsonProperty(PropertyName = "ClaimMapping", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "claimMapping", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DexOidcClaimMapping ClaimMapping { get; set; } = null;
    }
}