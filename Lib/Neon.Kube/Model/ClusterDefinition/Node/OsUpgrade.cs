//-----------------------------------------------------------------------------
// FILE:	    OsUpgrade.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible cluster node operating system upgrade options.
    /// </summary>
    public enum OsUpgrade
    {
        /// <summary>
        /// Perform no operating system upgrade.
        /// </summary>
        [EnumMember(Value = "none")]
        None,

        /// <summary>
        /// Upgrades many but not all components.  This is equivalent to performing: <b>apt-get upgrade</b>
        /// </summary>
        [EnumMember(Value = "partial")]
        Partial,

        /// <summary>
        /// Upgrades all components.  This is equivalent to performing: <b>apt-get dist-upgrade</b>
        /// </summary>
        [EnumMember(Value = "full")]
        Full
    }
}
