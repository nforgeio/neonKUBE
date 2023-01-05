//-----------------------------------------------------------------------------
// FILE:	    Resolution.cs
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Resolution determines how the proxy will resolve the IP addresses of the network endpoints associated with the service, so 
    /// that it can route to one of them. The resolution mode specified here has no impact on how the application resolves the IP 
    /// address associated with the service. The application may still have to use DNS to resolve the service to an IP so that the 
    /// outbound traffic can be captured by the Proxy. Alternatively, for HTTP services, the application could directly communicate 
    /// with the proxy (e.g., by setting HTTP_PROXY) to talk to these services.
    /// </summary>
    [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
    public enum Resolution
    {
        /// <summary>
        /// Assume that incoming connections have already been resolved (to a specific destination IP address). Such connections are
        /// typically routed via the proxy using mechanisms such as IP table REDIRECT/ eBPF. After performing any routing related 
        /// transformations, the proxy will forward the connection to the IP address to which the connection was bound.
        /// </summary>
        [EnumMember(Value = "NONE")]
        None = 0,

        /// <summary>
        /// Use the static IP addresses specified in endpoints (see below) as the backing instances associated with the service.
        /// </summary>
        [EnumMember(Value = "STATIC")]
        Static,

        /// <summary>
        /// Attempt to resolve the IP address by querying the ambient DNS, asynchronously. If no endpoints are specified, the 
        /// proxy will resolve the DNS address specified in the hosts field, if wildcards are not used. If endpoints are specified,
        /// the DNS addresses specified in the endpoints will be resolved to determine the destination IP address. DNS resolution
        /// cannot be used with Unix domain socket endpoints.
        /// </summary>
        [EnumMember(Value = "DNS")]
        Dns,

        /// <summary>
        /// Attempt to resolve the IP address by querying the ambient DNS, asynchronously. Unlike DNS, DNSROUNDROBIN only uses 
        /// the first IP address returned when a new connection needs to be initiated without relying on complete results of DNS 
        /// resolution and connections made to hosts will be retained even if DNS records change frequently eliminating draining 
        /// connection pools and connection cycling. This is best suited for large web scale services that must be accessed via DNS. 
        /// The proxy will resolve the DNS address specified in the hosts field, if wildcards are not used. DNS resolution cannot
        /// be used with Unix domain socket endpoints.
        /// </summary>
        [EnumMember(Value = "DNS_ROUND_ROBIN")]
        DnsRoundRobin
    }
}
