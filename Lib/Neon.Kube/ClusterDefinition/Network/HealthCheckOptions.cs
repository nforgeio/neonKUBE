//-----------------------------------------------------------------------------
// FILE:	    HealthCheckOptions.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// <para>
    /// Specifies health check settings for cluster <see cref="IngressRule"/> rules.
    /// </para>
    /// <note>
    /// Health check settings are currently honored only for clusters hosted in cloud
    /// environments.  You'll need to manually configure your router for on-premise
    /// clusters.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE clusters deployed to cloud hosting environments include a cloud provided
    /// load balancer that routes external network traffic to cluster nodes.  Currently,
    /// only TCP routing is supported.  HTTP and HTTPS traffic is routed as TCP because
    /// we don't want to add the complexity of managing TLS certificates at the load balancer
    /// and for ingress rules, we really want Isto to handle Cluster routing internally.
    /// UDP is not currently supported by Isto, so we don't support it either.
    /// </para>
    /// <para>
    /// You can use this to specify default health check options in <see cref="NetworkOptions"/>
    /// or you can override default options for specific <see cref="IngressRule"/> rules.
    /// </para>
    /// <para>
    /// Load balancers perform health checks at the interval specified by <see cref="IntervalSeconds"/>, 
    /// which defaults to <b>10 seconds</b>.  The health check method is simple: the load balancer
    /// simply tries to establish a TCP connection at the target port on the node.  The application
    /// is considered healthy when a connection can be established.
    /// </para>
    /// <para>
    /// The load balancer considers a node endpoint to be unhealthy when at least <see cref="ThresholdCount"/> 
    /// consecutive health checks have failed.  This defaults to <b>2</b>.  The load balancer will
    /// stop sending traffic to unhealthy node endpoints until they become healthy again.
    /// </para>
    /// </remarks>
    public class HealthCheckOptions
    {
        /// <summary>
        /// Specifies the interval in seconds between load balancer health checks.  This 
        /// defaults to <b>10 seconds</b> and must be in the range of <b>10...300</b>
        /// seconds.
        /// </summary>
        [JsonProperty(PropertyName = "IntervalSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "intervalSeconds", ApplyNamingConventions = false)]
        [DefaultValue(10)]
        public int IntervalSeconds { get; set; } = 10;

        /// <summary>
        /// Specifies the number of consecutive failed health checks before the load balancer
        /// will consider the node endpoint to be unhealthy.  This defaults to <b>2</b> and
        /// must be in the range of <b>2...10</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ThresholdCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "thresholdCount", ApplyNamingConventions = false)]
        [DefaultValue(2)]
        public int ThresholdCount { get; set; } = 2;

        /// <summary>
        /// Verifies the health settings.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="name">Used to identify where the health check property being checked originated when reporting errors.</param>
        /// <exception cref="ClusterDefinitionException">Thrown for invalid settings.</exception>
        public void Validate(ClusterDefinition clusterDefinition, string name)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (IntervalSeconds < 10 || 300 < IntervalSeconds)
            {
                throw new ClusterDefinitionException($"[{nameof(HealthCheckOptions)}.{nameof(HealthCheckOptions.IntervalSeconds)}={IntervalSeconds}] from [{name}] is outside the range of supported values [10...300] seconds.");
            }

            if (ThresholdCount < 2 || 10 < ThresholdCount)
            {
                throw new ClusterDefinitionException($"[{nameof(HealthCheckOptions)}.{nameof(HealthCheckOptions.ThresholdCount)}={ThresholdCount}] is [{name}] outside the range of supported values [2...10] seconds.");
            }
        }
    }
}
