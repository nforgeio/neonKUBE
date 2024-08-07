//-----------------------------------------------------------------------------
// FILE:        KubernetesOptions.cs
// CONTRIBUTOR: Jeff Lill
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes the options for the Kubernetes Kubelet service deployed
    /// on all cluster nodes.
    /// </summary>
    public class KubernetesOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubernetesOptions()
        {
        }

        /// <summary>
        /// Enables or disables specific Kubernetes features.  This can be used to enable
        /// alpha quality or other features that are disabled by default for the Kubernetes
        /// version being deployed or to disable features.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a dictionary that maps feature names a boolean where <c>true</c>
        /// enables the feature and <c>false</c> disables it.  You can find a description
        /// of the available Kubernetes feature gates here:
        /// </para>
        /// <para>
        /// https://kubernetes.io/docs/reference/command-line-tools-reference/feature-gates/#feature-gates
        /// </para>
        /// <para>
        /// NeonKUBE clusters enable specific features by default when you you haven't
        /// explicitly disabled them via this property.  Note that some features are 
        /// required and cannot be disabled.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>EphemeralContainers</b></term>
        ///     <description>
        ///     Enables the ability to add ephemeral containers to running pods.
        ///     This comes in handy for debugging running pods.
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "FeatureGates", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "featureGates", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, bool> FeatureGates = new Dictionary<string, bool>();

        /// <summary>
        /// Enable pods to be scheduled on cluster control-plane nodes.  This defaults to <c>null</c>
        /// which will allow pods to be scheduled on control-plane nodes if the cluster consists only of
        /// control-plane nodes (e.g. for a single node cluster).  This defaults to <c>false</c> for
        /// clusters with worker nodes.
        /// </summary>
        [JsonProperty(PropertyName = "AllowPodsOnControlPlane", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "allowPodsOnControlPlane", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? AllowPodsOnControlPlane { get; set; } = null;

        /// <summary>
        /// Specifies the maximum number of Pods that can be scheduled on a node. This defaults to: <b>250</b>
        /// </summary>
        [JsonProperty(PropertyName = "MaxPodsPerNode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "maxPodsPerNode", ApplyNamingConventions = false)]
        [DefaultValue(250)]
        public int MaxPodsPerNode { get; set; } = 250;

        /// <summary>
        /// Specifies seconds Kubelet will delay node shutdown while gracefully terminating pods
        /// on the node.  This is expressed in seconds and must be at least <b>30 seconds</b>.  This
        /// defaults to:<b>360 seconds</b>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Here's the Kubernetes documentation for this: https://kubernetes.io/docs/concepts/architecture/nodes/#graceful-node-shutdown
        /// <list type="bullet">
        /// <item>https://kubernetes.io/docs/concepts/architecture/nodes/#graceful-node-shutdown</item>
        /// <item>https://kubernetes.io/blog/2021/04/21/graceful-node-shutdown-beta/</item>
        /// <item>https://kubernetes.io/docs/reference/config-api/kubelet-config.v1beta1/#kubelet-config-k8s-io-v1beta1-KubeletConfiguration</item>
        /// <item>https://www.freedesktop.org/wiki/Software/systemd/inhibit/</item>
        /// </list>
        /// </para>
        /// <para>
        /// It appears that when Kubelet detects that the node is being shutdown it tries to gracefully
        /// shutdown pods like this:
        /// </para>
        /// <list type="number">
        /// <item>
        /// Pods are signaled to shutdown in <b>PriorityClass</b> order from lowest priority
        /// first, up to but not including critical pods.  These pods will be given up to
        /// <see cref="ShutdownGracePeriodSeconds"/> to stop gracefully before they may be
        /// forcibly terminated.
        /// </item>
        /// <item>
        /// After <see cref="ShutdownGracePeriodSeconds"/> minus- <see cref="ShutdownGracePeriodCriticalPodsSeconds"/>
        /// has elapsed since Kubelet detected node shutdown or all non-cr<b>PriorityClass</b> ordeitical pods have been stopped, 
        /// Kubelet will start shutting down critical pods in <b>PriorityClass</b> order.
        /// </item>
        /// <item>
        /// Kubelet will inhibit the kernel from shutting down the node until all pods have
        /// been shutdown or <see cref="ShutdownGracePeriodSeconds"/> has elapsed.  Once
        /// either of these conditions are true, Kubelet will release this lock so that
        /// the node can continue shutting down.
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "ShutdownGracePeriodSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "shutdownGracePeriodSeconds", ApplyNamingConventions = false)]
        [DefaultValue(360)]
        public int ShutdownGracePeriodSeconds { get; set; } = 360;

        /// <summary>
        /// Specifies the seconds that Kubelet will delay node shutdown for critical pods.  This
        /// defaults to <b>120 seconds</b> and must be less than <see cref="ShutdownGracePeriodSeconds"/>
        /// and not less than <b>30 seconds</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Here's the Kubernetes documentation for this: https://kubernetes.io/docs/concepts/architecture/nodes/#graceful-node-shutdown
        /// <list type="bullet">
        /// <item>https://kubernetes.io/docs/concepts/architecture/nodes/#graceful-node-shutdown</item>
        /// <item>https://kubernetes.io/blog/2021/04/21/graceful-node-shutdown-beta/</item>
        /// <item>https://kubernetes.io/docs/reference/config-api/kubelet-config.v1beta1/#kubelet-config-k8s-io-v1beta1-KubeletConfiguration</item>
        /// <item>https://www.freedesktop.org/wiki/Software/systemd/inhibit/</item>
        /// </list>
        /// </para>
        /// <para>
        /// It appears that when Kubelet detects that the node is being shutdown it tries to gracefully
        /// shutdown pods like this:
        /// </para>
        /// <list type="number">
        /// <item>
        /// Pods are signaled to shutdown in <b>PriorityClass</b> order from lowest priority
        /// first, up to but not including critical pods.  These pods will be given up to
        /// <see cref="ShutdownGracePeriodSeconds"/> to stop gracefully before they may be
        /// forcibly terminated.
        /// </item>
        /// <item>
        /// After <see cref="ShutdownGracePeriodSeconds"/> minus- <see cref="ShutdownGracePeriodCriticalPodsSeconds"/>
        /// has elapsed since Kubelet detected node shutdown or all non-cr<b>PriorityClass</b> ordeitical pods have been stopped, 
        /// Kubelet will start shutting down critical pods in <b>PriorityClass</b> order.
        /// </item>
        /// <item>
        /// Kubelet will inhibit the kernel from shutting down the node until all pods have
        /// been shutdown or <see cref="ShutdownGracePeriodSeconds"/> has elapsed.  Once
        /// either of these conditions are true, Kubelet will release this lock so that
        /// the node can continue shutting down.
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "ShutdownGracePeriodCriticalPodsSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "shutdownGracePeriodCriticalPodsSeconds", ApplyNamingConventions = false)]
        [DefaultValue(120)]
        public int ShutdownGracePeriodCriticalPodsSeconds { get; set; } = 120;

        /// <summary>
        /// <para>
        /// Used to reserve system resources for Linux System related services.
        /// See <a href="https://kubernetes.io/docs/tasks/administer-cluster/reserve-compute-resources/">Reserve Compute Resources</a>
        /// for more information.
        /// </para>
        /// <para>
        /// This defaults to an <b>empty map</b> to use the Kubernetes defaults.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "SystemReserved", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "systemReserved", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> SystemReserved { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// <para>
        /// Used to reserve system resources for Kubernetes related services.
        /// See <a href="https://kubernetes.io/docs/tasks/administer-cluster/reserve-compute-resources/">Reserve Compute Resources</a>
        /// for more information.
        /// </para>
        /// <para>
        /// This defaults to an <b>empty map</b> to use the Kubernetes defaults.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "KubeReserved", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeReserved", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> KubeReserved { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// <para>
        /// Used to specify hard eviction thresholds that Kubelet will use to evict pods with our
        /// a grace period.  See <a href="https://kubernetes.io/docs/concepts/scheduling-eviction/node-pressure-eviction/#hard-eviction-thresholds">Hard eviction thresholds</a>
        /// for more information.
        /// </para>
        /// <para>
        /// This defaults to an <b>empty map</b> to use the Kubernetes defaults.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "EvictionHard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "evictionHard", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> EvictionHard { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Specifies Kubernetes API Server options.
        /// </summary>
        [JsonProperty(PropertyName = "ApiServer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "apiServer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ApiServerOptions ApiServer { get; set; }

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values, as required.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Kubernetes)}";

            ApiServer ??= new ApiServerOptions();

            // Add default NeonKUBE feature gates when the user has not already configured them.

            FeatureGates = FeatureGates ?? new Dictionary<string, bool>();

            var requiredFeatures = new Dictionary<string, bool>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var feature in requiredFeatures)
            {
                if (!FeatureGates.ContainsKey(feature.Key) || FeatureGates[feature.Key] != feature.Value)
                {
                    FeatureGates[feature.Key] = feature.Value;
                }
            }

            if (!AllowPodsOnControlPlane.HasValue)
            {
                AllowPodsOnControlPlane = clusterDefinition.Workers.Count() == 0;
            }

            EvictionHard   = EvictionHard ?? new Dictionary<string, string>();
            SystemReserved = SystemReserved ?? new Dictionary<string, string>();
            KubeReserved   = KubeReserved ?? new Dictionary<string, string>();

            if (ShutdownGracePeriodSeconds < 30)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(ShutdownGracePeriodSeconds)}={ShutdownGracePeriodSeconds}] cannot be less than 30 seconds.");
            }

            if (ShutdownGracePeriodCriticalPodsSeconds < 30)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(ShutdownGracePeriodCriticalPodsSeconds)}={ShutdownGracePeriodCriticalPodsSeconds}] cannot be less than 30 seconds.");
            }

            if (ShutdownGracePeriodCriticalPodsSeconds >= ShutdownGracePeriodSeconds)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(ShutdownGracePeriodCriticalPodsSeconds)}={ShutdownGracePeriodCriticalPodsSeconds}] must be less than [{nameof(ShutdownGracePeriodSeconds)}={ShutdownGracePeriodSeconds}].");
            }

            var podSubnetCidr = NetworkCidr.Parse(clusterDefinition.Network.PodSubnet);

            if (clusterDefinition.Nodes.Count() * MaxPodsPerNode * 2.3 > podSubnetCidr.UsableAddressCount)
            {
                var maxPods        = podSubnetCidr.UsableAddressCount / 2.3;
                var clusterPods    = clusterDefinition.Nodes.Count() * MaxPodsPerNode;
                var maxPodsPerNode = maxPods / clusterDefinition.Nodes.Count();
                var maxNodes       = maxPods / MaxPodsPerNode;

                throw new ClusterDefinitionException(@$"[{optionsPrefix}.{nameof(MaxPodsPerNode)}={MaxPodsPerNode}] is not valid.

[{optionsPrefix}.{nameof(clusterDefinition.Network.PodSubnet)}={clusterDefinition.Network.PodSubnet}] supports a maximum of {maxPods} pods.

Either expand: [{optionsPrefix}.{nameof(clusterDefinition.Network.PodSubnet)}], decrease [{optionsPrefix}.{nameof(MaxPodsPerNode)}] to [{maxPodsPerNode}], 
or decrease:   [{optionsPrefix}.{nameof(clusterDefinition.Nodes)}] to [{maxNodes}].
");
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemServices))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.SystemServices = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Labels.SystemServices = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemDbServices))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.SystemDbServices = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Labels.SystemDbServices = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemRegistryServices))
            {
                foreach (var manager in clusterDefinition.ControlNodes)
                {
                    manager.Labels.SystemRegistryServices = true;
                }

                if (clusterDefinition.ControlNodes.Count() < 3)
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Labels.SystemRegistryServices = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemIstioServices))
            {
                if (AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.SystemIstioServices = true;
                    };
                }
                else
                {
                    foreach (var worker in clusterDefinition.Nodes.Where(node => node.IsWorker))
                    {
                        worker.Labels.SystemIstioServices = true;
                    }
                }
            }

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemOpenEbsStorage))
            {
                if (AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.SystemOpenEbsStorage = true;
                    };
                }
                else
                {
                    foreach (var worker in clusterDefinition.Nodes.Where(node => node.IsWorker))
                    {
                        worker.Labels.SystemOpenEbsStorage = true;
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
