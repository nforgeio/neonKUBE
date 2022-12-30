//-----------------------------------------------------------------------------
// FILE:	    DevLinkType.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.OpenEBS
{
    /// <summary>
    /// Enumerates the possible device link types.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum DevLinkType
    {
        /// <summary>
        /// Device links listed by ID.
        /// </summary>
        [EnumMember(Value = "by-id")]
        ById = 0,

        /// <summary>
        /// Device links listed by path.
        /// </summary>
        [EnumMember(Value = "by-path")]
        ByPath
    }
}
