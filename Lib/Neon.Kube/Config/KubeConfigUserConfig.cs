//-----------------------------------------------------------------------------
// FILE:	    KubeConfigUserConfig.cs
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

using k8s.KubeConfigModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Describes a Kubernetes user's credentials.
    /// </summary>
    public class KubeConfigUserConfig
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigUserConfig()
        {
        }

        /// <summary>
        /// The optional path to the client certificate for TLS.
        /// </summary>
        [JsonProperty(PropertyName = "client-certificate", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-certificate", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientCertificate { get; set; }

        /// <summary>
        /// Optionally specifies the contains PEM-encoded data from a client cert file for TLS.  Overrides <see cref="ClientCertificate"/>.
        /// </summary>
        [JsonProperty(PropertyName = "client-certificate-data", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-certificate-data", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientCertificateData { get; set; }

        /// <summary>
        /// Optionally specifies the path to a client key file for TLS.
        /// </summary>
        [JsonProperty(PropertyName = "client-key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-key", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientKey { get; set; }

        /// <summary>
        /// Optionally specifies PEM-encoded data from a client key file for TLS.  Overrides <see cref="ClientKey"/>.
        /// </summary>
        [JsonProperty(PropertyName = "client-key-data", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "client-key-data", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ClientKeyData { get; set; }

        /// <summary>
        /// Optionally specifies the bearer token for authentication to the kubernetes cluster.
        /// </summary>
        [JsonProperty(PropertyName = "token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "token", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Token { get; set; }

        /// <summary>
        /// Optionaliiy specifies file path to a bearer token for authentication to the kubernetes cluster.  If
        /// both <see cref="Token"/> and <see cref="TokenFile"/> are present, <see cref="Token"/> takes precedence.
        /// </summary>
        [JsonProperty(PropertyName = "token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "token", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string TokenFile { get; set; }

        /// <summary>
        /// Optionally specifies the username to impersionate.
        /// </summary>
        [JsonProperty(PropertyName = "as", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "as", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string As { get; set; }

        /// <summary>
        /// Optionally specifies the user ID to impersionate.
        /// </summary>
        [JsonProperty(PropertyName = "as-uid", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "as-uid", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string AsUid { get; set; }

        /// <summary>
        /// Optionally specifies the group IDs to impersionate.
        /// </summary>
        [JsonProperty(PropertyName = "as-groups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "as-groups", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string[] AsGroups { get; set; }

        /// <summary>
        /// Optionally specifies the additional imperionation information.
        /// </summary>
        [JsonProperty(PropertyName = "as-user-extra", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "as-user-extra", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public Dictionary<string, string> AsUserExtra { get; set; }

        /// <summary>
        /// Optionally specifies the username.
        /// </summary>
        [JsonProperty(PropertyName = "username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// Optionally specifies the password.
        /// </summary>
        [JsonProperty(PropertyName = "password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Optionally specifies a custom authentication provider plugin.
        /// </summary>
        [JsonProperty(PropertyName = "auth-provider", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "auth-provider", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public KubeConfigAuthProvider AuthProvider { get; set; }

        /// <summary>
        /// Optionally specifies a custom exec-based authentication plugin.
        /// </summary>
        /// <summary>
        /// Optionally specifies a custom authentication provider plugin.
        /// </summary>
        [JsonProperty(PropertyName = "exec", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "exec", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public KubeConfigExecConfig Exec { get; set; }

        /// <summary>
        /// Lists any custom extension properties.  Extensions are name/value pairs added
        /// by vendors to hold arbitrary information.  Take care to choose property names
        /// that are unlikely to conflict with properties created by other vendors by adding
        /// a custom suffix like <b>my-property.neonkube.io</b>, where <b>my-property</b> 
        /// identifies the property and <b>neonkibe.io</b> helps avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "extensions", Required = Required.Default)]
        [YamlMember(Alias = "extensions", ApplyNamingConventions = false)]
        public List<NamedExtension> Extensions { get; set; }
    }
}
