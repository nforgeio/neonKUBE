//-----------------------------------------------------------------------------
// FILE:	    DnsEndpoint.cs
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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;

using k8s;
using k8s.Models;

namespace Neon.Kube.Resources.ExternalDns
{
    /// <summary>
    /// Enumerates the possible Block Device types.
    /// </summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum DnsRecordType
    {
        /// <summary>
        /// A.
        /// </summary>
        [EnumMember(Value = "A")]
        A = 0,

        /// <summary>
        /// NS.
        /// </summary>
        [EnumMember(Value = "NS")]
        NS,

        /// <summary>
        /// CNAME.
        /// </summary>
        [EnumMember(Value = "CNAME")]
        CNAME,

        /// <summary>
        /// SOA.
        /// </summary>
        [EnumMember(Value = "SOA")]
        SOA,

        /// <summary>
        /// WKS.
        /// </summary>
        [EnumMember(Value = "WKS")]
        WKS,

        /// <summary>
        /// PTR.
        /// </summary>
        [EnumMember(Value = "PTR")]
        PTR,

        /// <summary>
        /// MX.
        /// </summary>
        [EnumMember(Value = "MX")]
        MX,

        /// <summary>
        /// AAAA.
        /// </summary>
        [EnumMember(Value = "AAAA")]
        AAAA,

        /// <summary>
        /// TXT.
        /// </summary>
        [EnumMember(Value = "TXT")]
        TXT,

        /// <summary>
        /// SRV.
        /// </summary>
        [EnumMember(Value = "SRV")]
        SRV,

        /// <summary>
        /// OPT.
        /// </summary>
        [EnumMember(Value = "OPT")]
        OPT
    }
}
