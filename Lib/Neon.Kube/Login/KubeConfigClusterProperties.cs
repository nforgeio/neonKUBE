//-----------------------------------------------------------------------------
// FILE:	    KubeConfigClusterProperties.cs
// CONTRIBUTOR: Jeff Lill
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

namespace Neon.Kube
{
    /// <summary>
    /// Describes a Kubernetes cluster's properties.
    /// </summary>
    public class KubeConfigClusterProperties
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigClusterProperties()
        {
        }

        /// <summary>
        /// Fully qualified URL to the cluster's API server.
        /// </summary>
        [JsonProperty(PropertyName = "server", Required = Required.Always)]
        [YamlMember(Alias = "server", ApplyNamingConventions = false)]
        public string Server { get; set; }

        /// <summary>
        /// Optional path to the cluster certificate authority file.
        /// </summary>
        [JsonProperty(PropertyName = "certificate-authority-data", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificate-authority-data", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CertificateAuthorityData { get; set; }

        /// <summary>
        /// Optionally disables TLS verification of the server.
        /// </summary>
        [JsonProperty(PropertyName = "insecure-skip-tls-verify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecure-skip-tls-verify", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool InsecureSkipTlsVerify { get; set; } = false;
    }
}
