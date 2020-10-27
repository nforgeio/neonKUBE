//-----------------------------------------------------------------------------
// FILE:	    KubernetesOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net.Http;
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
    /// Describes the Kubernetes options for a neonKUBE.
    /// </summary>
    public class KubernetesOptions
    {
        private const string minVersion              = "1.13.0";
        private const string defaultVersion          = "default";
        private const string defaultDashboardVersion = "default";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubernetesOptions()
        {
        }

        /// <summary>
        /// The version of Kubernetes to be installed.  This defaults to <b>default</b> which
        /// will install the latest tested version of Kubernetes.  The minimum supported
        /// version is <b>1.13.0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "version", ApplyNamingConventions = false)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// The version of Kubernetes dashboard to be installed.  This defaults to <b>default</b> which
        /// will install the latest tested version of Kubernetes.
        /// </summary>
        [JsonProperty(PropertyName = "DashboardVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dashboardVersion", ApplyNamingConventions = false)]
        [DefaultValue(defaultVersion)]
        public string DashboardVersion { get; set; } = defaultDashboardVersion;

        /// <summary>
        /// The version of Helm to be installed.  This defaults to <b>default</b> which
        /// will install a reasonable version for the Kubernetes release being inbstalled.
        /// </summary>
        [JsonProperty(PropertyName = "HelmVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helmVersion", ApplyNamingConventions = false)]
        [DefaultValue("default")]
        public string HelmVersion { get; set; } = "default";

        /// <summary>
        /// Enable pods to be scheduled on cluster master nodes.  This defaults to <c>null</c>
        /// which will allow pods to be scheduled on masters if the cluster consists only of
        /// master nodes (e.g. for a single node cluster.  This defaults to <c>false</c> for
        /// clusters with worker nodes.
        /// </summary>
        [JsonProperty(PropertyName = "AllowPodsOnMasters", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "allowPodsOnMasters", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? AllowPodsOnMasters { get; set; } = null;

        /// <summary>
        /// Optionally configures an external Kubernetes API server load balancer by
        /// specifying the load balancer endpoint as HOSTNAME:PORT or IPADDRESS:PORT.
        /// This defaults to <c>null</c>.  See the remarks to see what this means.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Production clusters really should be deployed using an external highly
        /// available load balancer that distributes API server traffic across
        /// the API servers running on the masters.
        /// </para>
        /// <para>
        /// For cloud environments like AWS and Azure, neonKUBE provisions a cloud
        /// load balancer by default for this.  This is the ideal situation.
        /// </para>
        /// <para>
        /// For on-premise environments like Hyper-V and XenServer, we use the
        /// HAProxy based load balancer deployed to the first master node (as sorted
        /// by node name).  This forwards traffic to port 5000 to the Kubernetes
        /// API servers running on the masters.  This is not reeally HA though,
        /// because the loss of the first master will result in the loss of 
        /// API server connectivity.  This does help some though.  For example,
        /// stopping the API server on the first master won't take the cluster
        /// API server offline because HAProxy will still be able to direct 
        /// traffic to the remaining masters.
        /// </para>
        /// <note>
        /// <para>
        /// The HAProxy load balancer is actually deployed to all of the masters
        /// but the other master HAProxy instances won't see any traffic because
        /// Kubernetes is configured with a single balancer endpoint.
        /// </para>
        /// <para>
        /// In the future, it may be possible to turn the master HAProxy instances
        /// into an HA cluster via a virtual IP address and heartbeat mechanism.
        /// </para>
        /// <para>
        /// You can use the <see cref="ApiLoadBalancer"/> property to specify an
        /// external load balancer that already exists.  Setting this will override
        /// the default behaviors described above.
        /// </para>
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "apiLoadBalancer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "apiLoadBalancer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ApiLoadBalancer { get; set; } = null;

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

            Version = Version ?? defaultVersion;
            Version = Version.ToLowerInvariant();

            if (Version != defaultVersion)
            {
                if (!System.Version.TryParse(Version, out var vKubernetes))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(Version)}={Version}] is not a valid Kubernetes version.");
                }

                if (vKubernetes < System.Version.Parse(minVersion))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(Version)}={Version}] is less than the supported version [{minVersion}].");
                }
            }

            if (DashboardVersion != defaultDashboardVersion)
            {
                if (!System.Version.TryParse(DashboardVersion, out var vDashboard))
                {
                    throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(DashboardVersion)}={DashboardVersion}] is not a valid version number.");
                }
            }

            if (HelmVersion != "default" && !System.Version.TryParse(HelmVersion, out var vHelm))
            {
                throw new ClusterDefinitionException($"[{nameof(KubernetesOptions)}.{nameof(HelmVersion)}={HelmVersion}] is invalid].");
            }

            if (!string.IsNullOrEmpty(ApiLoadBalancer))
            {
                // Ensure that this specifies a HOSTNAME:PORT or IPADDRESS:PORT.

                var fields = ApiLoadBalancer.Split(':', 2);
                var error  = $"[{nameof(KubernetesOptions)}.{nameof(ApiLoadBalancer)}={ApiLoadBalancer}] is invalid].";

                if (!NetHelper.IsValidHost(fields[0]) || !NetHelper.TryParseIPv4Address(fields[0], out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ClusterDefinitionException(error);
                }

                if (!int.TryParse(fields[1], out var port) || !NetHelper.IsValidPort(port))
                {
                    throw new ClusterDefinitionException(error);
                }
            }

            if (!AllowPodsOnMasters.HasValue)
            {
                AllowPodsOnMasters = clusterDefinition.Workers.Count() == 0;
            }

            if (!clusterDefinition.Nodes.Any(n => n.Labels.NeonSystemDb))
            {
                foreach (var m in clusterDefinition.Masters)
                {
                    m.Labels.NeonSystemDb = true;
                }
            }

            if (!clusterDefinition.Nodes.Any(n => n.Labels.NeonSystemRegistry))
            {
                foreach (var m in clusterDefinition.Masters)
                {
                    m.Labels.NeonSystemRegistry = true;
                }
            }

            if (!clusterDefinition.Nodes.Any(n => n.Labels.Istio))
            {
                if (AllowPodsOnMasters.GetValueOrDefault())
                {
                    foreach (var n in clusterDefinition.Nodes)
                    {
                        n.Labels.Istio = true;
                    };
                }
                else
                {
                    foreach (var w in clusterDefinition.Nodes.Where(n => n.IsWorker))
                    {
                        w.Labels.Istio = true;
                    }
                }
            }
        }

        /// <summary>
        /// Clears any sensitive properties like the Docker registry credentials.
        /// </summary>
        public void ClearSecrets()
        {
        }
    }
}
