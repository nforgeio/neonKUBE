//-----------------------------------------------------------------------------
// FILE:	    KubeFileDetails.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
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
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Holds the contents and permissions for a downloaded Kubernetes file.
    /// </summary>
    public class KubeFileDetails
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeFileDetails()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="text">The file contexts.</param>
        /// <param name="permissions">Optional file permissions (defaults to <b>600</b>).</param>
        /// <param name="owner">Optional file owner (defaults to <b>root:root</b>).</param>
        public KubeFileDetails(string text, string permissions = "600", string owner = "root:root")
        {
            this.Text        = text;
            this.Permissions = permissions;
            this.Owner       = owner;
        }

        /// <summary>
        /// The file text.
        /// </summary>
        [JsonProperty(PropertyName = "Text", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Text", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Text { get; set; }

        /// <summary>
        /// The file permissions.
        /// </summary>
        [JsonProperty(PropertyName = "Permissions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Permissions", ApplyNamingConventions = false)]
        public string Permissions { get; set; }

        /// <summary>
        /// The file owner.
        /// </summary>
        [JsonProperty(PropertyName = "Owner", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Owner", ApplyNamingConventions = false)]
        public string Owner { get; set; }
    }
}
