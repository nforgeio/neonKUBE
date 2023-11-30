//-----------------------------------------------------------------------------
// FILE:        KubeConfigExecInteractiveMode.cs
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
using System.Runtime.Serialization;
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
using Neon.Kube;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Describes an <see cref="KubeConfigAuthProvider"/>'s relationshit with standard input.
    /// </summary>
    public enum KubeConfigExecInteractiveMode
    {
        /// <summary>
        /// Indicates that the plugin will never use standard input.
        /// </summary>
        [EnumMember(Value = "Never")]
        Never,

        /// <summary>
        /// Indicates that the plugin will use standard input when available.
        /// </summary>
        [EnumMember(Value = "IfAvailable")]
        IfAvailable,

        /// <summary>
        /// Indicates that the plugin will always use standard input.
        /// </summary>
        [EnumMember(Value = "Always")]
        Always
    }
}
