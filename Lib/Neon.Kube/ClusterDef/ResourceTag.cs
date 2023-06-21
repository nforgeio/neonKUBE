//-----------------------------------------------------------------------------
// FILE:        ResourceTag.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes a tag that can be attached to resources for clusters deployed to 
    /// a cloud.
    /// </summary>
    public class ResourceTag
    {
        /// <summary>
        /// Constructs a tag with a name and optional value.
        /// </summary>
        /// <param name="key">The tag key.</param>
        /// <param name="value">The optional tag value.  Note that empty value strings will be converted to <c>null</c>.</param>
        public ResourceTag(string key, string value = null)
        {
            this.Key = key;

            if (!string.IsNullOrEmpty(value))
            {
                this.Value = value;
            }
            else
            {
                this.Value = null;
            }
        }

        /// <summary>
        /// Returns the tag key.
        /// </summary>
        public string Key { get; private set; }

        /// <summary>
        /// Returns the tag value or <c>null</c>.
        /// </summary>
        public string Value { get; private set; }
    }
}
