//-----------------------------------------------------------------------------
// FILE:        Server.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the properties of the proxy on a given load balancer port.
    /// </summary>
    public class Server
    {
        /// <summary>
        /// Initializes a new instance of the Server class.
        /// </summary>
        public Server()
        {
        }

        /// <summary>
        /// The Port on which the proxy should listen for incoming connections.
        /// </summary>
        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public Port Port { get; set; }

        /// <summary>
        /// The ip or the Unix domain socket to which the listener should be bound to. Format: x.x.x.x or unix:///path/to/uds or 
        /// unix://@foobar (Linux abstract namespace). When using Unix domain sockets, the port number should be 0. This can be used to 
        /// restrict the reachability of this server to be gateway internal only. This is typically used when a gateway needs to 
        /// communicate to another mesh service e.g. publishing metrics. In such case, the server created with the specified bind will 
        /// not be available to external gateway clients.
        /// </summary>
        [JsonProperty(PropertyName = "bind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Bind { get; set; }

        /// <summary>
        /// <para>
        /// One or more hosts exposed by this gateway. While typically applicable to HTTP services, it can also be used for TCP services 
        /// using TLS with SNI. A host is specified as a dnsName with an optional namespace/ prefix. The dnsName should be specified using 
        /// FQDN format, optionally including a wildcard character in the left-most component (e.g., prod/*.example.com). Set the dnsName 
        /// to * to select all V1VirtualService hosts from the specified namespace (e.g.,prod/*).
        /// </para>
        /// <para>
        /// The namespace can be set to * or ., representing any or the current namespace, respectively. For example, */foo.example.com 
        /// selects the service from any available namespace while ./foo.example.com only selects the service from the namespace of the 
        /// sidecar. The default, if no namespace/ is specified, is */, that is, select services from any namespace. Any associated 
        /// DestinationRule in the selected namespace will also be used.
        /// </para>
        /// <para>
        /// A V1VirtualService must be bound to the gateway and must have one or more hosts that match the hosts specified in a server. 
        /// The match could be an exact match or a suffix match with the server’s hosts. For example, if the server’s hosts specifies 
        /// *.example.com, a V1VirtualService with hosts dev.example.com or prod.example.com will match. However, a V1VirtualService with 
        /// host example.com or newexample.com will not match.
        /// </para>
        /// <para>
        /// NOTE: Only virtual services exported to the gateway’s namespace (e.g., exportTo value of *) can be referenced. Private 
        /// configurations (e.g., exportTo set to .) will not be available. Refer to the exportTo setting in V1VirtualService, DestinationRule, 
        /// and ServiceEntry configurations for details.</para>
        /// </summary>
        [JsonProperty(PropertyName = "hosts", Required = Required.Always)]
        public List<string> Hosts { get; set; }

        /// <summary>
        /// Set of TLS related options that govern the server’s behavior. Use these options to control if all http requests should be 
        /// redirected to https, and the TLS modes to use.
        /// </summary>
        [JsonProperty(PropertyName = "tls", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ServerTLSSettings TLS { get; set; }

        /// <summary>
        /// An optional name of the server, when set must be unique across all servers. This will be used for variety of purposes like 
        /// prefixing stats generated with this name etc.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }
    }
}
