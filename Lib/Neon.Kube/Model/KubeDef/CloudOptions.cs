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

namespace Neon.Kube
{
    /// <summary>
    /// Describes cloud related cluster settings.
    /// </summary>
    public class CloudOptions
    {
        private const int defaultSshIngressStart = 64000;
        private const int defaultSshIngressEnd   = 64999;

        /// <summary>
        /// <para>
        /// Specifies the start of a range of ingress load balancer ports reserved by
        /// neonKUBE for temporarily exposing SSH from individual cluster nodes to the
        /// Internet during cluster setup as well as afterwards so that a cluster node
        /// can be accessed remotely by a cluster operator.
        /// </para>
        /// <note>
        /// The number ports between <see cref="SshIngressStart"/> and <see cref="SshIngressEnd"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 1,000 nodes, which is a big number.  This default range is unlikely to conflict with 
        /// ports a cluster may need to expliitly expose to the Internet like HTTP/HTTPS (80/443).
        /// You can change this range for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SshIngressStart", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshIngressStart", ApplyNamingConventions = false)]
        [DefaultValue(defaultSshIngressStart)]
        public int SshIngressStart { get; set; } = defaultSshIngressStart;

        /// <summary>
        /// <para>
        /// Specifies the end of a range of ingress load balancer ports reserved by
        /// neonKUBE for temporarily exposing SSH from individual cluster nodes to the
        /// Internet during cluster setup as well as afterwards so that a cluster node
        /// can be accessed remotely by a cluster operator.
        /// </para>
        /// <note>
        /// The number ports between <see cref="SshIngressStart"/> and <see cref="SshIngressEnd"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 1,000 nodes, which is a big number.  This default range is unlikely to conflict with 
        /// ports a cluster may need to expliitly expose to the Internet like HTTP/HTTPS (80/443).
        /// You can change this range for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SshIngressEnd", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshIngressEnd", ApplyNamingConventions = false)]
        [DefaultValue(defaultSshIngressEnd)]
        public int SshIngressEnd { get; set; } = defaultSshIngressEnd;

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

            if (SshIngressStart >= SshIngressEnd)
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(SshIngressStart)}={SshIngressStart}]-[{nameof(SshIngressEnd)}={SshIngressEnd}] range.  [{nameof(SshIngressStart)}] must be greater than [{nameof(SshIngressEnd)}].");
            }

            if (SshIngressStart - SshIngressEnd < clusterDefinition.Nodes.Count())
            {
                throw new ClusterDefinitionException($"[{nameof(SshIngressStart)}]-[{nameof(SshIngressEnd)}] range is not large enough to support the [{clusterDefinition.Nodes.Count()}] cluster nodes.");
            }
        }
    }
}
