//-----------------------------------------------------------------------------
// FILE:	    KubeConfigUser.cs
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
    /// Describes a Kubernetes user configuration.
    /// </summary>
    public class KubeConfigUser
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigUser()
        {
        }

        /// <summary>
        /// The local nickname for the user.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        /// <summary>
        /// The user properties.
        /// </summary>
        [JsonProperty(PropertyName = "user", Required = Required.Always)]
        [YamlMember(Alias = "user")]
        public KubeConfigUserProperties Properties { get; set; }
    }
}
