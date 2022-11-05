//-----------------------------------------------------------------------------
// FILE:	    HTTPMethod.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates HTTP method types.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumMemberConverter))]
    public enum HTTPMethod
    {
        /// <summary>
        /// GET
        /// </summary>
        [EnumMember(Value = "GET")]
        GET = 0,

        /// <summary>
        /// HEAD
        /// </summary>
        [EnumMember(Value = "HEAD")]
        HEAD,

        /// <summary>
        /// POST
        /// </summary>
        [EnumMember(Value = "POST")]
        POST,

        /// <summary>
        /// PUT
        /// </summary>
        [EnumMember(Value = "PUT")]
        PUT,

        /// <summary>
        /// DELETE
        /// </summary>
        [EnumMember(Value = "DELETE")]
        DELETE,

        /// <summary>
        /// CONNECT
        /// </summary>
        [EnumMember(Value = "CONNECT")]
        CONNECT,

        /// <summary>
        /// OPTIONS
        /// </summary>
        [EnumMember(Value = "OPTIONS")]
        OPTIONS,

        /// <summary>
        /// TRACE
        /// </summary>
        [EnumMember(Value = "TRACE")]
        TRACE,

        /// <summary>
        /// PATCH
        /// </summary>
        [EnumMember(Value = "PATCH")]
        PATCH
    }
}
