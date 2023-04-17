//-----------------------------------------------------------------------------
// FILE:	    KubeFileDetails.cs
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
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Holds the contents and permissions for a downloaded Kubernetes text file.
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
        [YamlMember(Alias = "text", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Text { get; set; }

        /// <summary>
        /// The file permissions.
        /// </summary>
        [JsonProperty(PropertyName = "Permissions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "permissions", ApplyNamingConventions = false)]
        public string Permissions { get; set; }

        /// <summary>
        /// The file owner.
        /// </summary>
        [JsonProperty(PropertyName = "Owner", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "owner", ApplyNamingConventions = false)]
        public string Owner { get; set; }
    }
}
