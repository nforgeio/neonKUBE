//-----------------------------------------------------------------------------
// FILE:	    X509Usages.cs
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
    /// X509Usages controls how private keys should be regenerated when a re-issuance is being processed.
    /// </summary>
    public enum X509Usages
    {
        /// <summary>
        /// Signing
        /// </summary>
        [EnumMember(Value = "signing")]
        Signing = 0,

        /// <summary>
        /// Digital Signature
        /// </summary>
        [EnumMember(Value = "digital signature")]
        DigitalSignature,

        /// <summary>
        /// Content Commitment
        /// </summary>
        [EnumMember(Value = "content commitment")]
        ContentCommitment,

        /// <summary>
        /// Key Encipherment 
        /// </summary>
        [EnumMember(Value = "key encipherment")]
        KeyEncipherment,

        /// <summary>
        /// Key Agreement
        /// </summary>
        [EnumMember(Value = "key agreement")]
        KeyAgreement,

        /// <summary>
        /// Data Encipherment
        /// </summary>
        [EnumMember(Value = "data encipherment")]
        DataEncipherment,

        /// <summary>
        /// Cert Sign
        /// </summary>
        [EnumMember(Value = "cert sign")]
        CertSign,

        /// <summary>
        /// Crl Sign
        /// </summary>
        [EnumMember(Value = "crl sign")]
        CrlSign,

        /// <summary>
        /// Encipher Only
        /// </summary>
        [EnumMember(Value = "encipher only")]
        EncipherOnly,

        /// <summary>
        /// Decipher Only
        /// </summary>
        [EnumMember(Value = "decipher only")]
        DecipherOnly,

        /// <summary>
        /// Any 
        /// </summary>
        [EnumMember(Value = "any")]
        Any,

        /// <summary>
        /// Server Auth
        /// </summary>
        [EnumMember(Value = "server auth")]
        ServerAuth,

        /// <summary>
        /// Client Auth
        /// </summary>
        [EnumMember(Value = "client auth")]
        ClientAuth,

        /// <summary>
        /// Code Signing
        /// </summary>
        [EnumMember(Value = "code signing")]
        CodeSigning,

        /// <summary>
        /// Email Protection
        /// </summary>
        [EnumMember(Value = "email protection")]
        EmailProtection,

        /// <summary>
        /// S/MIME
        /// </summary>
        [EnumMember(Value = "s/mime")]
        SMIME,

        /// <summary>
        /// IPSEC End System
        /// </summary>
        [EnumMember(Value = "ipsec end system")]
        IpsecEndSystem,

        /// <summary>
        /// IPSEC Tunnel
        /// </summary>
        [EnumMember(Value = "ipsec tunnel")]
        IpsecTunnel,

        /// <summary>
        /// IPSEC User.
        /// </summary>
        [EnumMember(Value = "ipsec user")]
        IpsecUser,

        /// <summary>
        /// Timestamping
        /// </summary>
        [EnumMember(Value = "timestamping")]
        Timestamping,

        /// <summary>
        /// Ocsp Signing
        /// </summary>
        [EnumMember(Value = "ocsp signing")]
        OcspSigning,

        /// <summary>
        /// Microsoft Sgc
        /// </summary>
        [EnumMember(Value = "microsoft sgc")]
        MicrosoftSgc,

        /// <summary>
        /// Netscape Sgc
        /// </summary>
        [EnumMember(Value = "netscape sgc")]
        NetscapeSgc
    }
}
