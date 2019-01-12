//-----------------------------------------------------------------------------
// FILE:	    KubeConfigUserProperties.cs
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
    /// Describes a Kubernetes user's credentials.
    /// </summary>
    public class KubeConfigUserProperties
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigUserProperties()
        {
        }

        /// <summary>
        /// The optional authentication token (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Token { get; set; }

        /// <summary>
        /// The optional path to the client certificate (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "client-certificate", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ClientCertificate { get; set; }

        /// <summary>
        /// The optional path to the client key (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "client-key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ClientKey { get; set; }

        /// <summary>
        /// The optional username (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The optional password (or <c>null</c>).
        /// </summary>
        [JsonProperty(PropertyName = "password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Password { get; set; }
    }
}
