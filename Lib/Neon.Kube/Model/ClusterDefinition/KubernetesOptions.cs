//-----------------------------------------------------------------------------
// FILE:	    KubernetesOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Net;

using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

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
        /// Enable pods to be scheduled on cluster control-plane nodes.  This defaults to <c>null</c>
        /// which will allow pods to be scheduled on control-plane nodes if the cluster consists only of
        /// control-plane nodes (e.g. for a single node cluster.  This defaults to <c>false</c> for
        /// clusters with worker nodes.
        /// </summary>
        [JsonProperty(PropertyName = "AllowPodsOnControlPlane", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "allowPodsOnControlPlane", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? AllowPodsOnControlPlane { get; set; } = null;

        /// <summary>
        /// The maximum number of Pods that can run on this Kubelet. The value must be a non-negative integer. If DynamicKubeletConfig 
        /// (deprecated; default off) is on, when dynamically updating this field, consider that changes may cause Pods to fail admission on 
        /// Kubelet restart, and may change the value reported in Node.Status.Capacity[v1.ResourcePods], thus affecting future scheduling decisions.
        /// Increasing this value may also decrease performance, as more Pods can be packed into a single node. Default: 250
        /// </summary>
        [JsonProperty(PropertyName = "MaxPodsPerNode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "maxPodsPerNode", ApplyNamingConventions = false)]
        [DefaultValue(250)]
        public int MaxPodsPerNode { get; set; } = 250;

        /// <summary>
        /// A set of ResourceName=ResourceQuantity (e.g. cpu=200m,memory=150G) pairs that describe resources reserved for non-kubernetes components. 
        /// Currently only cpu and memory are supported. See http://kubernetes.io/docs/user-guide/compute-resources for more detail. Default: nil
        /// </summary>
        [JsonProperty(PropertyName = "SystemReserved", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "systemReserved", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> SystemReserved { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// A set of ResourceName=ResourceQuantity (e.g. cpu=200m,memory=150G) pairs that describe resources reserved for kubernetes system components.
        /// Currently cpu, memory and local storage for root file system are supported. 
        /// See https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/ for more details. Default: nil
        /// </summary>
        [JsonProperty(PropertyName = "KubeReserved", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeReserved", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> KubeReserved { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// A is a map of signal names to quantities that defines hard eviction thresholds. For example: {"memory.available": "300Mi"}. 
        /// To explicitly disable, pass a 0% or 100% threshold on an arbitrary resource. 
        /// Default: memory.available: "100Mi" nodefs.available: "10%" nodefs.inodesFree: "5%" imagefs.available: "15%"
        /// </summary>
        [JsonProperty(PropertyName = "EvictionHard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "evictionHard", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> EvictionHard { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var kubernetesOptionsPrefix = $"{nameof(ClusterDefinition.Kubernetes)}";

            Version = Version ?? defaultVersion;
            Version = Version.ToLowerInvariant();

            if (Version != defaultVersion)
            {
                if (!System.Version.TryParse(Version, out var kubernetesVersion))
                {
                    throw new ClusterDefinitionException($"[{kubernetesOptionsPrefix}.{nameof(Version)}={Version}] is not a valid Kubernetes version.");
                }

                if (kubernetesVersion < System.Version.Parse(minVersion))
                {
                    throw new ClusterDefinitionException($"[{kubernetesOptionsPrefix}.{nameof(Version)}={Version}] is less than the supported version [{minVersion}].");
                }
            }

            if (DashboardVersion != defaultDashboardVersion)
            {
                if (!System.Version.TryParse(DashboardVersion, out var vDashboard))
                {
                    throw new ClusterDefinitionException($"[{kubernetesOptionsPrefix}.{nameof(DashboardVersion)}={DashboardVersion}] is not a valid version number.");
                }
            }

            if (HelmVersion != "default" && !System.Version.TryParse(HelmVersion, out var vHelm))
            {
                throw new ClusterDefinitionException($"[{kubernetesOptionsPrefix}.{nameof(HelmVersion)}={HelmVersion}] is invalid].");
            }

            if (!AllowPodsOnControlPlane.HasValue)
            {
                AllowPodsOnControlPlane = clusterDefinition.Workers.Count() == 0;
            }

            var controlPlaneMemory = (decimal)clusterDefinition.ControlNodes.First().Vm.GetMemory(clusterDefinition);

            if (EvictionHard == null
                || !EvictionHard.ContainsKey("memory.available"))
            {
                EvictionHard["memory.available"] = 
                    new ResourceQuantity(
                        controlPlaneMemory * 0.05m, 
                        0, 
                        ResourceQuantity.SuffixFormat.BinarySI)
                    .CanonicalizeString();
            }

            if (SystemReserved == null
                || !SystemReserved.ContainsKey("memory"))
            {
                var evictionHard = new ResourceQuantity(EvictionHard["memory.available"]);

                SystemReserved["memory"] =
                    new ResourceQuantity(
                        (controlPlaneMemory * 0.05m) + evictionHard.ToDecimal(),
                        0,
                        ResourceQuantity.SuffixFormat.BinarySI)
                    .CanonicalizeString();
            }

            if (KubeReserved == null
                || !KubeReserved.ContainsKey("memory"))
            {
                KubeReserved["memory"] =
                    new ResourceQuantity(
                        controlPlaneMemory * 0.05m,
                        0,
                        ResourceQuantity.SuffixFormat.BinarySI)
                    .CanonicalizeString();
            }

            var podSubnetCidr = NetworkCidr.Parse(clusterDefinition.Network.PodSubnet);

            if ((clusterDefinition.Nodes.Count() * MaxPodsPerNode * 2.3) > podSubnetCidr.UsableAddressCount)
            {
                var maxPods        = podSubnetCidr.UsableAddressCount / 2.3;
                var clusterPods    = clusterDefinition.Nodes.Count() * MaxPodsPerNode;
                var maxPodsPerNode = maxPods / clusterDefinition.Nodes.Count();
                var maxNodes       = maxPods / MaxPodsPerNode;

                throw new ClusterDefinitionException(@$"[{kubernetesOptionsPrefix}.{nameof(MaxPodsPerNode)}={MaxPodsPerNode}] is not valid.
[{kubernetesOptionsPrefix}.{nameof(clusterDefinition.Network.PodSubnet)}={clusterDefinition.Network.PodSubnet}] supports a maximum of {maxPods} pods.
Either expand [{kubernetesOptionsPrefix}.{nameof(clusterDefinition.Network.PodSubnet)}], decrease [{kubernetesOptionsPrefix}.{nameof(MaxPodsPerNode)}] to [{maxPodsPerNode}], 
or decrease [{kubernetesOptionsPrefix}.{nameof(clusterDefinition.Nodes)}] to [{maxNodes}].
");
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.NeonSystem))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.NeonSystem = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var w in clusterDefinition.Workers)
                    {
                        w.Labels.NeonSystem = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.NeonSystemDb))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.NeonSystemDb = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Labels.NeonSystemDb = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.NeonSystemRegistry))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.NeonSystemRegistry = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Labels.NeonSystemRegistry = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.Istio))
            {
                if (AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.Istio = true;
                    };
                }
                else
                {
                    foreach (var worker in clusterDefinition.Nodes.Where(node => node.IsWorker))
                    {
                        worker.Labels.Istio = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.OpenEBS))
            {
                if (AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.OpenEBS = true;
                    };
                }
                else
                {
                    foreach (var worker in clusterDefinition.Nodes.Where(node => node.IsWorker))
                    {
                        worker.Labels.OpenEBS = true;
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
