//-----------------------------------------------------------------------------
// FILE:	    CloudOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;
using System.Xml;

namespace Neon.Kube
{
    /// <summary>
    /// Describes cloud related cluster settings.
    /// </summary>
    public class CloudOptions
    {
        private const int defaultReservedIngressStartPort = 64000;
        private const int defaultReservedIngressEndPort   = 64999;
        private const int additionalReservedPorts         = 100;

        /// <summary>
        /// <para>
        /// Specifies the start of a range of ingress load balancer ports reserved by
        /// neonKUBE.  These are reserved for temporarily exposing SSH from individual 
        /// cluster nodes to the Internet during cluster setup as well as afterwards so 
        /// that a cluster node can be accessed remotely by a cluster operator as well
        /// as for other purposes and for potential future features such as an integrated
        /// VPN.
        /// </para>
        /// <note>
        /// The number ports between <see cref="ReservedIngressStartPort"/> and <see cref="ReservedIngressEndPort"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster
        /// for the temporary SSH NAT rules plus another 100 ports reserved for other purposes.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 900 nodes.  This default range is unlikely to conflict with ports a cluster is likely
        /// to need expose to the Internet like HTTP/HTTPS (80/443).  You can change this range 
        /// for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ReservedIngressStartPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "reservedIngressStartPort", ApplyNamingConventions = false)]
        [DefaultValue(defaultReservedIngressStartPort)]
        public int ReservedIngressStartPort { get; set; } = defaultReservedIngressStartPort;

        /// <summary>
        /// <para>
        /// Specifies the end of a range of ingress load balancer ports reserved by
        /// neonKUBE.  These are reserved for temporarily exposing SSH from individual 
        /// cluster nodes to the Internet during cluster setup as well as afterwards so 
        /// that a cluster node can be accessed remotely by a cluster operator as well
        /// as for other purposes and for potential future features such as an integrated
        /// </para>
        /// <note>
        /// The number ports between <see cref="ReservedIngressStartPort"/> and <see cref="ReservedIngressEndPort"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster
        /// for the temporary SSH NAT rules plus another 100 ports reserved for other purposes.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 900 nodes.  This default range is unlikely to conflict with ports a cluster is likely
        /// to need expose to the Internet like HTTP/HTTPS (80/443).  You can change this range 
        /// for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ReservedIngressEndPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "reservedIngressEndPort", ApplyNamingConventions = false)]
        [DefaultValue(defaultReservedIngressEndPort)]
        public int ReservedIngressEndPort { get; set; } = defaultReservedIngressEndPort;

        /// <summary>
        /// Returns the port number for the reserved management SSH ingress NAT rule.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int FirstSshIngressPort => ReservedIngressStartPort + additionalReservedPorts;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (!NetHelper.IsValidPort(ReservedIngressStartPort))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressStartPort)}={ReservedIngressStartPort}] port.");
            }

            if (!NetHelper.IsValidPort(ReservedIngressEndPort))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressEndPort)}={ReservedIngressEndPort}] port.");
            }

            if (ReservedIngressStartPort >= ReservedIngressEndPort)
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressStartPort)}={ReservedIngressStartPort}]-[{nameof(ReservedIngressEndPort)}={ReservedIngressEndPort}] range.  [{nameof(ReservedIngressStartPort)}] must be greater than [{nameof(ReservedIngressEndPort)}].");
            }

            if (ReservedIngressEndPort - ReservedIngressStartPort + additionalReservedPorts < clusterDefinition.Nodes.Count())
            {
                throw new ClusterDefinitionException($"[{nameof(ReservedIngressStartPort)}]-[{nameof(ReservedIngressEndPort)}] range is not large enough to support [{clusterDefinition.Nodes.Count()}] cluster nodes in addition to [{additionalReservedPorts}] additional reserved ports.");
            }
        }
    }
}
