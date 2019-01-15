//-----------------------------------------------------------------------------
// FILE:	    KubeConfigContextProperties.cs
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
    /// Describes a Kubernetes context properties.
    /// </summary>
    public class KubeConfigContextProperties
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigContextProperties()
        {
        }

        /// <summary>
        /// The optional cluster nickname.
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cluster")]
        [DefaultValue(null)]
        public string Cluster { get; set; }

        /// <summary>
        /// The optional namespace.
        /// </summary>
        [JsonProperty(PropertyName = "namespace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "namespace")]
        [DefaultValue(null)]
        public string Namespace { get; set; }

        /// <summary>
        /// The optional user nickname.
        /// </summary>
        [JsonProperty(PropertyName = "user", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "user")]
        [DefaultValue(null)]
        public string User { get; set; }

        /// <summary>
        /// Specifies neonKUBE related extensions associated with the context.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeContextExtension Extensions { get; set; }
    }
}
