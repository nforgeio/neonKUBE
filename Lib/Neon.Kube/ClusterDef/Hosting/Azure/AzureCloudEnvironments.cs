//-----------------------------------------------------------------------------
// FILE:        AzureCloudEnvironments.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Enumerates the possible Azure hosting environments.
    /// </summary>
    public enum AzureCloudEnvironments
    {
        /// <summary>
        /// Public Azure cloud (default).
        /// </summary>
        [EnumMember(Value = "global-cloud")]
        GlobalCloud = 0,

        /// <summary>
        /// Custom cloud where the management URIs
        /// will be specified explicitly.
        /// </summary>
        [EnumMember(Value = "custom")]
        Custom,

        /// <summary>
        /// China cloud.
        /// </summary>
        [EnumMember(Value = "china")]
        ChinaCloud,

        /// <summary>
        /// German cloud.
        /// </summary>
        [EnumMember(Value = "german")]
        GermanCloud,

        /// <summary>
        /// United States Government cloud.
        /// </summary>
        [EnumMember(Value = "us-government")]
        USGovernment
    }
}
