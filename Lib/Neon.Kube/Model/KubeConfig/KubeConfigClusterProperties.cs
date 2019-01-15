//-----------------------------------------------------------------------------
// FILE:	    KubeConfigClusterProperties.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
        [YamlMember(Alias = "server")]
        public string Server { get; set; }

        /// <summary>
        /// Optional path to the cluster certificate authority file.
        /// </summary>
        [JsonProperty(PropertyName = "certificate-authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificate-authority")]
        [DefaultValue(null)]
        public string CertificateAuthority { get; set; }

        /// <summary>
        /// Optionally disables TLS verification of the server.
        /// </summary>
        [JsonProperty(PropertyName = "insecure-skip-tls-verify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "insecure-skip-tls-verify")]
        [DefaultValue(false)]
        public bool InsecureSkipTlsVerify { get; set; } = false;
    }
}
