//-----------------------------------------------------------------------------
// FILE:	    KubeConfigClusterConfig.cs
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
    /// Describes a Kubernetes cluster's configuration.
    /// </summary>
    public class KubeConfigClusterConfig
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigClusterConfig()
        {
        }

        /// <summary>
        /// Specifies the qualified URL to the cluster's API server.
        /// </summary>
        [JsonProperty(PropertyName = "server", Required = Required.Always)]
        [YamlMember(Alias = "server", ApplyNamingConventions = false)]
        public string Server { get; set; }

        /// <summary>
        /// Optionally used to check server certificate.  If <see cref="TlsServerName"/> is empty, the hostname used to contact the server is used.
        /// </summary>
        [JsonProperty(PropertyName = "tls-server-name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tls-server-name", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string TlsServerName { get; set; }

        /// <summary>
        /// Optionally disables TLS verification of the server.
        /// </summary>
        [JsonProperty(PropertyName = "insecure-skip-tls-verify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecure-skip-tls-verify", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(false)]
        public bool InsecureSkipTlsVerify { get; set; } = false;

        /// <summary>
        /// Optional path to the cluster certificate authority file.
        /// </summary>
        [JsonProperty(PropertyName = "certificate-authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificate-authority", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string CertificateAuthority { get; set; }

        /// <summary>
        /// Optionally specifies the PRM-encode certificate authority certificates. Overrides <see cref="CertificateAuthority"/>.
        /// </summary>
        [JsonProperty(PropertyName = "certificate-authority-data", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificate-authority-data", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string CertificateAuthorityData { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies is the URL to the proxy to be used for all requests made by this client. 
        /// URLs with "http", "https", and "socks5" schemes are supported. If this configuration is no
        /// provided or the empty string, the client attempts to construct a proxy configuration from
        /// http_proxy and https_proxy environment variables. If these environment variables are not set,
        /// the client does not attempt to proxy requests.
        /// </para>
        /// <para>
        /// socks5 proxying does not currently support spdy streaming endpoints (exec, attach, port forward).
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "proxy-url", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "proxy-url", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(null)]
        public string ProxyUrl { get; set; }

        /// <summary>
        /// <para>
        /// Optionally allows client to opt-out of response compression for all requests to the server. 
        /// This is useful to speed up requests (specifically lists) when client-server network bandwidth
        /// is ample, by saving time on compression (server-side) and decompression (client-side): 
        /// </para>
        /// <para>
        /// https://github.com/kubernetes/kubernetes/issues/112296.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "disable-compression", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "disable-compression", ApplyNamingConventions = false, DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
        [DefaultValue(false)]
        public bool DisableCompression { get; set; } = false;

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
