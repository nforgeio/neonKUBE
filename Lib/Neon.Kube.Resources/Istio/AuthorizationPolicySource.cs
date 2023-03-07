//-----------------------------------------------------------------------------
// FILE:	    AuthorizationPolicySource.cs
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
    /// Matches requests from a list of sources that perform a list of operations subject to a list of conditions.
    /// A match occurs when at least one source, one operation and all conditions matches the request. 
    /// An empty rule is always matched.
    /// </summary>
    public class AuthorizationPolicySource
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AuthorizationPolicySource()
        {

        }

        /// <summary>
        /// <para>
        /// A list of peer identities derived from the peer certificate. The peer identity is in the format 
        /// of "TRUST_DOMAIN/ns/NAMESPACE/sa/SERVICE_ACCOUNT", for example, "cluster.local/ns/default/sa/productpage".
        /// This field requires mTLS enabled and is the same as the source.principal attribute.
        /// </para>
        /// <remarks>
        /// If not set, any principal is allowed.
        /// </remarks>
        /// </summary>
        public List<string> Principals { get; set; } = null;

        /// <summary>
        /// A list of negative match of peer <see cref="Principals"/>.
        /// </summary>
        public List<string> NotPrincipals { get; set; } = null;

        /// <summary>
        /// <para>
        /// A list of request identities derived from the JWT. The request identity is in the format 
        /// of "ISS/SUB", for example, "example.com/sub-1". This field requires request authentication 
        /// enabled and is the same as the request.auth.principal attribute.
        /// </para>
        /// <remarks>
        /// If not set, any request principal is allowed.
        /// </remarks>
        /// </summary>
        public List<string> RequestPrincipals { get; set; } = null;

        /// <summary>
        /// A list of negative match of request <see cref="RequestPrincipals"/>.
        /// </summary>
        public List<string> NotRequestPrincipals { get; set; } = null;

        /// <summary>
        /// <para>
        /// A list of namespaces derived from the peer certificate. This field requires mTLS enabled and is the 
        /// same as the source.namespace attribute.
        /// </para>
        /// <remarks>
        /// If not set, any namespace is allowed.
        /// </remarks>
        /// </summary>
        public List<string> Namespaces { get; set; } = null;

        /// <summary>
        /// A list of negative match of <see cref="Namespaces"/>.
        /// </summary>
        public List<string> NotNamespaces { get; set; } = null;

        /// <summary>
        /// <para>
        /// A list of IP blocks, populated from the source address of the IP packet. Single IP 
        /// (e.g. “1.2.3.4”) and CIDR (e.g. “1.2.3.0/24”) are supported. This is the same as the 
        /// source.ip attribute.
        /// </para>
        /// <remarks>
        /// If not set, any IP is allowed.
        /// </remarks>
        /// </summary>
        public List<string> IpBlocks { get; set; } = null;

        /// <summary>
        /// A list of negative match of <see cref="IpBlocks"/>.
        /// </summary>
        public List<string> NotIpBlocks { get; set; } = null;

        /// <summary>
        /// <para>
        /// A list of IP blocks, populated from X-Forwarded-For header or proxy protocol. To make
        /// use of this field, you must configure the numTrustedProxies field of the gatewayTopology 
        /// under the meshConfig when you install Istio or using an annotation on the ingress gateway. 
        /// See the documentation here: Configuring Gateway Network Topology. Single IP 
        /// (e.g. “1.2.3.4”) and CIDR (e.g. “1.2.3.0/24”) are supported. This is the same as the 
        /// remote.ip attribute.
        /// </para>
        /// <remarks>
        /// If not set, any IP is allowed.
        /// </remarks>
        /// </summary>
        public List<string> RemoteIpBlocks { get; set; } = null;

        /// <summary>
        /// A list of negative match of <see cref="RemoteIpBlocks"/>.
        /// </summary>
        public List<string> NotRemoteIpBlocks { get; set; } = null;
    }
}
