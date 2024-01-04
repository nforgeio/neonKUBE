//-----------------------------------------------------------------------------
// FILE:        Oauth2ProxyProviders.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
    /// Oauth2Proxy providers model.
    /// </summary>
    public class Oauth2ProxyProvider
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public Oauth2ProxyProvider()
        {
        }

        /// <summary>
        /// The OAuth Client ID that is defined in the provider
        /// This value is required for all providers.
        /// </summary>
        [JsonProperty(PropertyName = "ClientId", Required = Required.Always)]
        [YamlMember(Alias = "clientID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClientId { get; set; }

        /// <summary>
        /// The OAuth Client Secret that is defined in the provider
        /// This value is required for all providers.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSecret", Required = Required.Always)]
        [YamlMember(Alias = "clientSecret", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClientSecret { get; set; }

        /// <summary>
        /// The name of the file containing the OAuth Client Secret, it will be used if ClientSecret is not set.
        /// This value is required for all providers.
        /// </summary>
        [JsonProperty(PropertyName = "ClientSecretFile", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "clientSecretFile", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientSecretFile { get; set; }

        /// <summary>
        /// ID should be a unique identifier for the provider.
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Always)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public string Id { get; set; }

        /// <summary>
        /// The OAuth provider type. <see cref="Oauth2ProxyProviderType"/>
        /// </summary>
        [JsonProperty(PropertyName = "Provider", Required = Required.Always)]
        [YamlMember(Alias = "provider", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyProviderType Provider { get; set; }

        /// <summary>
        /// The providers display name if set, it will be shown to the users in the login page.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// A list of paths to CA certificates that should be used when connecting to the provider.
        /// </summary>
        /// <remarks>
        /// If not specified, the default Go trust sources are used instead
        /// </remarks>
        [JsonProperty(PropertyName = "CaFiles", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "caFiles", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(false)]
        public List<string> CaFiles { get; set; }

        /// <summary>
        /// The authentication endpoint
        /// </summary>
        [JsonProperty(PropertyName = "LoginUrl", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "loginURL", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LoginUrl { get; set; }

        /// <summary>
        /// Defines the parameters that can be passed from the start URL to the IdP login URL
        /// </summary>
        [JsonProperty(PropertyName = "LoginUrlParameters", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "loginURLParameters", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public List<Oauth2ProxyLoginUrlParameters> LoginUrlParameters { get; set; }

        /// <summary>
        /// The token redemption endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "RedeemUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "redeemURL", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string RedeemUrl { get; set; }

        /// <summary>
        /// The profile access endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "ProfileUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "profileURL", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ProfileUrl { get; set; }

        /// <summary>
        /// The profile access endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "Resource", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "resource", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Resource { get; set; }

        /// <summary>
        /// The access token validation endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "ValidateUrl", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "validateURL", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ValidateUrl { get; set; }

        /// <summary>
        /// The access token validation endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "Scope", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "scope", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Scope { get; set; }

        /// <summary>
        /// A list of restrict logins to members of this group.
        /// </summary>
        [JsonProperty(PropertyName = "AllowedGroups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "allowedGroups", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public List<string> AllowedGroups { get; set; }

        /// <summary>
        /// The access token validation endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "CodeChallengeMethod", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "code_challenge_method", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string CodeChallengeMethod { get; set; }

        /// <summary>
        /// Holds all configurations for OIDC provider or providers utilize OIDC configurations.
        /// </summary>
        [JsonProperty(PropertyName = "OidcConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Ignore)]
        [YamlMember(Alias = "oidcConfig", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Oauth2ProxyOidcOptions OidcConfig { get; set; }
    }
}
