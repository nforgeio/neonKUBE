//-----------------------------------------------------------------------------
// FILE:        AuthorizationPolicyRule.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Matches requests from a list of sources that perform a list of operations subject to a list of conditions.
    /// A match occurs when at least one source, one operation and all conditions matches the request. 
    /// An empty rule is always matched.
    /// </summary>
    public class AuthorizationPolicyRule
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthorizationPolicyRule()
        {

        }

        /// <summary>
        /// Includes a list of sources.
        /// </summary>
        public class From
        {
            /// <summary>
            /// Specifies the source of a request.
            /// </summary>
            public AuthorizationPolicySource Source { get; set; } = null;
        }

        /// <summary>
        /// Includes a list of operations.
        /// </summary>
        public class To
        {
            /// <summary>
            /// Specifies the operation of a request.
            /// </summary>
            public AuthorizationPolicyOperation Operation { get; set; } = null;
        }
    }
}
