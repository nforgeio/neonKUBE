//-----------------------------------------------------------------------------
// FILE:        Oauth2ProxyProviderType.cs
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
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Oauth2Proxy
{
    /// <summary>
    /// Oauth2Proxy providers model.
    /// </summary>
    public enum Oauth2ProxyProviderType
    {
        /// <summary>
        /// ADFS
        /// </summary>
        [EnumMember(Value = "adfs")]
        Adfs,

        /// <summary>
        /// Azure
        /// </summary>
        [EnumMember(Value = "azure")]
        Azure,

        /// <summary>
        /// BitBucket
        /// </summary>
        [EnumMember(Value = "bitbucket")]
        BitBucket,

        /// <summary>
        /// DigitalOcean
        /// </summary>
        [EnumMember(Value = "digitalocean")]
        DigitalOcean,

        /// <summary>
        /// Facebook
        /// </summary>
        [EnumMember(Value = "facebook")]
        Facebook,

        /// <summary>
        /// GitHub
        /// </summary>
        [EnumMember(Value = "github")]
        GitHub,

        /// <summary>
        /// GitLab
        /// </summary>
        [EnumMember(Value = "gitlab")]
        GitLab,

        /// <summary>
        /// Google
        /// </summary>
        [EnumMember(Value = "google")]
        Google,

        /// <summary>
        /// Keycloak
        /// </summary>
        [EnumMember(Value = "keycloak")]
        Keycloak,

        /// <summary>
        /// KeyCloakOidc
        /// </summary>
        [EnumMember(Value = "keycloak-oidc")]
        KeycloakOidc,

        /// <summary>
        /// Linkedin
        /// </summary>
        [EnumMember(Value = "linkedin")]
        Linkedin,

        /// <summary>
        /// LoginGov
        /// </summary>
        [EnumMember(Value = "login.gov")]
        LoginGov,

        /// <summary>
        /// Nextcloud
        /// </summary>
        [EnumMember(Value = "nextcloud")]
        Nextcloud,

        /// <summary>
        /// OIDC
        /// </summary>
        [EnumMember(Value = "oidc")]
        Oidc,
    }
}
