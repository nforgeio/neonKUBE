//-----------------------------------------------------------------------------
// FILE:	    AuthorizationPolicyAction.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Action specifies the operation to take for an <see cref="V1AuthorizationPolicy"/>.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum AuthorizationPolicyAction
    {
        /// <summary>
        /// Allow a request only if it matches the rules. This is the default type.
        /// </summary>
        [EnumMember(Value = "ALLOW")]
        Allow = 0,

        /// <summary>
        /// Deny a request if it matches any of the rules.
        /// </summary>
        [EnumMember(Value = "DENY")]
        Deny,

        /// <summary>
        /// Audit a request if it matches any of the rules.
        /// </summary>
        [EnumMember(Value = "AUDIT")]
        Audit,

        /// <summary>
        /// <para>
        /// The CUSTOM action allows an extension to handle the user request if the matching rules evaluate to true. 
        /// The extension is evaluated independently and before the native ALLOW and DENY actions. When used together, 
        /// A request is allowed if and only if all the actions return allow, in other words, the extension cannot bypass 
        /// the authorization decision made by ALLOW and DENY action. Extension behavior is defined by the named providers 
        /// declared in MeshConfig. The authorization policy refers to the extension by specifying the name of the 
        /// provider. One example use case of the extension is to integrate with a custom external authorization system 
        /// to delegate the authorization decision to it.
        /// </para>
        /// </summary>
        [EnumMember(Value = "CUSTOM")]
        Custom,
    }
}
