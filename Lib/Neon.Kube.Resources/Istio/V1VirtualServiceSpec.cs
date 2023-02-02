//-----------------------------------------------------------------------------
// FILE:	    V1VirtualServiceSpec.cs
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

using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes a load balancer operating at the edge of the mesh receiving incoming or outgoing HTTP/TCP connections.
    /// </summary>
    public class V1VirtualServiceSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1VirtualServiceSpec()
        {
        }

        /// <summary>
        /// The destination hosts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The destination hosts to which traffic is being sent. Could be a DNS name with wildcard prefix or an IP address. Depending on the 
        /// platform, short-names can also be used instead of a FQDN (i.e. has no dots in the name). In such a scenario, the FQDN of the host 
        /// would be derived based on the underlying platform.
        /// </para>
        /// <para>
        /// A single V1VirtualService can be used to describe all the traffic properties of the corresponding hosts, including those for multiple
        /// HTTP and TCP ports. Alternatively, the traffic properties of a host can be defined using more than one V1VirtualService, with certain 
        /// caveats. Refer to the Operations Guide for details.
        /// </para>
        /// <para>
        /// Note for Kubernetes users: When short names are used(e.g. “reviews” instead of “reviews.default.svc.cluster.local”), Istio will 
        /// interpret the short name based on the namespace of the rule, not the service.A rule in the “default” namespace containing a host 
        /// “reviews” will be interpreted as “reviews.default.svc.cluster.local”, irrespective of the actual namespace associated with the reviews 
        /// service.To avoid potential misconfigurations, it is recommended to always use fully qualified domain names over short names.
        /// </para>
        /// <para>
        /// The hosts field applies to both HTTP and TCP services.Service inside the mesh, i.e., those found in the service registry, must always 
        /// be referred to using their alphanumeric names.IP addresses are allowed only for services defined via the Gateway.
        /// </para>
        /// <note>
        /// This must be empty for a delegate V1VirtualService.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "hosts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Hosts { get; set; }

        /// <summary>
        /// The names of gateways and sidecars that should apply these routes. Gateways in other namespaces may be referred to by 
        /// &lt;gateway namespace&gt;/&lt;gateway name&gt;; specifying a gateway with no namespace qualifier is the same as specifying the 
        /// V1VirtualService’s namespace. A single V1VirtualService is used for sidecars inside the mesh as well as for one or more gateways. The 
        /// selection condition imposed by this field can be overridden using the source field in the match conditions of protocol-specific routes. 
        /// The reserved word mesh is used to imply all the sidecars in the mesh. When this field is omitted, the default gateway (mesh) will be used,
        /// which would apply the rule to all sidecars in the mesh. If a list of gateway names is provided, the rules will apply only to the gateways. 
        /// To apply the rules to both gateways and sidecars, specify mesh as one of the gateway names.
        /// </summary>
        [JsonProperty(PropertyName = "gateways", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Gateways { get; set; }

        /// <summary>
        /// An ordered list of route rules for HTTP traffic. HTTP routes will be applied to platform service ports named ‘http-’/‘http2-’/‘grpc-*’, 
        /// gateway ports with protocol HTTP/HTTP2/GRPC/ TLS-terminated-HTTPS and service entry ports using HTTP/HTTP2/GRPC protocols. The first rule 
        /// matching an incoming request is used.
        /// </summary>
        [JsonProperty(PropertyName = "http", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<HTTPRoute> Http { get; set; }

        /// <summary>
        /// <para>
        /// An ordered list of route rule for non-terminated TLS and HTTPS traffic. Routing is typically performed using the SNI value presented by the 
        /// ClientHello message. TLS routes will be applied to platform service ports named <b>https-*</b>, <b>tls-*</b>, unterminated gateway ports using HTTPS/TLS 
        /// protocols (i.e. with passthrough TLS mode) and service entry ports using HTTPS/TLS protocols. The first rule matching an incoming request 
        /// is used. 
        /// </para>
        /// <note>
        /// Traffic <b>https-*</b> or <b>tls-*</b> ports without associated virtual service will be treated as opaque TCP traffic.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "tls", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<TLSRoute> TLS { get; set; }

        /// <summary>
        /// An ordered list of route rules for opaque TCP traffic. TCP routes will be applied to any port that is not a HTTP or TLS port. The first rule
        /// matching an incoming request is used.
        /// </summary>
        [JsonProperty(PropertyName = "tcp", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<TCPRoute> TCP { get; set; }

        /// <summary>
        /// <para>
        /// A list of namespaces to which this virtual service is exported. Exporting a virtual service allows it to be used by sidecars and gateways 
        /// defined in other namespaces. This feature provides a mechanism for service owners and mesh administrators to control the visibility of virtual 
        /// services across namespace boundaries.
        /// </para>
        /// <para>
        /// If no namespaces are specified then the virtual service is exported to all namespaces by default.
        /// </para>
        /// <para>
        /// The value “.” is reserved and defines an export to the same namespace that the virtual service is declared in. Similarly the value “*” is 
        /// reserved and defines an export to all namespaces.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "exportTo", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> ExportTo { get; set; }
    }
}
