//-----------------------------------------------------------------------------
// FILE:	    TLSMode.cs
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

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// TLS modes enforced by the proxy.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum TLSMode
    {
        /// <summary>
        /// The SNI string presented by the client will be used as the match criterion in a VirtualService 
        /// TLS route to determine the destination service from the service registry.
        /// </summary>
        [EnumMember(Value = "PASSTHROUGH")]
        Passthrough = 0,

        /// <summary>
        /// Secure connections with standard TLS semantics.
        /// </summary>
        [EnumMember(Value = "SIMPLE")]
        Simple,

        /// <summary>
        /// Secure connections to the downstream using mutual TLS by presenting server certificates for authentication.
        /// </summary>
        [EnumMember(Value = "MUTUAL")]
        Mutual,

        /// <summary>
        /// <para>
        /// Similar to the passthrough mode, except servers with this TLS mode do not require an associated VirtualService 
        /// to map from the SNI value to service in the registry. The destination details such as the service/subset/port 
        /// are encoded in the SNI value. The proxy will forward to the upstream (Envoy) cluster (a group of endpoints) 
        /// specified by the SNI value. This server is typically used to provide connectivity between services in disparate 
        /// L3 networks that otherwise do not have direct connectivity between their respective endpoints. Use of this mode 
        /// assumes that both the source and the destination are using Istio mTLS to secure traffic. In order for this mode 
        /// to be enabled, the gateway deployment must be configured with the ISTIO_META_ROUTER_MODE=sni-dnat environment 
        /// variable.
        /// </para>
        /// </summary>
        [EnumMember(Value = "AUTO_PASSTHROUGH")]
        AutoPassthrough,

        /// <summary>
        /// <para>
        /// Secure connections from the downstream using mutual TLS by presenting server certificates for authentication. 
        /// Compared to Mutual mode, this mode uses certificates, representing gateway workload identity, generated automatically 
        /// by Istio for mTLS authentication. When this mode is used, all other fields in TLSOptions should be empty.
        /// </para>
        /// </summary>
        [EnumMember(Value = "ISTIO_MUTUAL")]
        IstioMutual
    }
}
