//-----------------------------------------------------------------------------
// FILE:	    KeyAlgorithm.cs
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

namespace Neon.Kube.Resources
{
    /// <summary>
    /// The private key algorithm of the corresponding private key for this certificate. If provided, 
    /// allowed values are either `RSA` or `ECDSA` If `algorithm` is specified and `size` is not provided,
    /// key size of 256 will be used for `ECDSA` key algorithm and key size of 2048 will be used for 
    /// `RSA` key algorithm.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum KeyAlgorithm
    {
        /// <summary>
        /// RSA#1
        /// </summary>
        [EnumMember(Value = "rsa")]
        RSA = 0,

        /// <summary>
        /// ECDSA#8
        /// </summary>
        [EnumMember(Value = "ecdsa")]
        ECDSA
    }
}
