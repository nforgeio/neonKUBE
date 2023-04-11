//-----------------------------------------------------------------------------
// FILE:	    ClusterChecker.cs
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
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.priority=
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Login;
using Neon.Kube.Setup;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Performs various checks on a cluster and writes any output to STDOUT.
    /// </summary>
    internal static class ClusterChecker
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to temporarily hold the priority information for a pod.
        /// </summary>
        private class PodPriorityInfo
        {
            /// <summary>
            /// Identifies the pod owner.
            /// </summary>
            public string Owner;

            /// <summary>
            /// The priority class value.
            /// </summary>
            public int? Priority;

            /// <summary>
            /// The priority class name.
            /// </summary>
            public string PriorityClassName;
        }

        /// <summary>
        /// Used to hold local status for container imnages.
        /// </summary>
        private class ImageStatus
        {
            /// <summary>
            /// The container image names.
            /// </summary>
            public string ImageNames;

            /// <summary>
            /// Set to <c>true</c> when the image isn't specified in the cluster manifest.
            /// </summary>
            public bool NotInManifest;
        }

        /// <summary>
        /// Used to hold information about container resource requests and limits.
        /// </summary>
        private class ContainerResources
        {
            /// <summary>
            /// Identifies the associated container image.
            /// </summary>
            public string ContainerImage { get; set; }

            /// <summary>
            /// Specifies the CPU request quantity or <c>null</c> when not set.
            /// </summary>
            public ResourceQuantity RequestCpu;

            /// <summary>
            /// Specifies the RAM request quantity or <c>null</c> when not set.
            /// </summary>
            public ResourceQuantity RequestMemory;

            /// <summary>
            /// Specifies the CPU limit quantity or <c>null</c> when not set.
            /// </summary>
            public ResourceQuantity LimitCpu;

            /// <summary>
            /// Specifies the RAM request quantity or <c>null</c> when not set.
            /// </summary>
            public ResourceQuantity LimitMemory;

            /// <summary>
            /// Returns <c>true</c> when any of the resource values are <c>null</c>.
            /// </summary>
            public bool Error => RequestCpu == null || RequestMemory == null || LimitCpu == null || LimitMemory == null;
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Performs development related cluster checks with information on potential
        /// problems being written to STDOUT.
        /// </summary>
        /// <param name="clusterLogin">Specifies the target cluster login.</param>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        public static async Task<bool> CheckAsync(ClusterLogin clusterLogin, IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            var error = false;

            if (!await CheckNodeContainerImagesAsync(clusterLogin, k8s))
            {
                error = true;
            }

            if (!await CheckPodPrioritiesAsync(clusterLogin, k8s))
            {
                error = true;
            }

            if (!await CheckResourcesAsync(clusterLogin, k8s))
            {
                error = true;
            }

            return error;
        }

        /// <summary>
        /// <para>
        /// Verifies that all of the container images currently loaded on nodes are specified in the
        /// container manifest.  Any images that aren't in the manifest need to be preloaded info the 
        /// node image.  This is used to ensure that pods started by third-party operators are also 
        /// included in the cluster manifest, ensuing that our node images are self-contained for a 
        /// better setup experience as well as air-gapped clusters.
        /// </para>
        /// <para>
        /// Details about any issues will be written to STDOUT.
        /// </para>
        /// </summary>
        /// <param name="clusterLogin">Specifies the target cluster login.</param>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <param name="details">Optionally specifies that status should be written to STDOUT when there's no errors.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        /// <remarks>
        /// <para>
        /// neonKUBE clusters deploy all required images to CRI-O running on all cluster
        /// nodes as well as the local Harbor registry.  This not only improves the cluster
        /// setup experience but also makes air gapped cluster possible.
        /// </para>
        /// </remarks>
        public static async Task<bool> CheckNodeContainerImagesAsync(ClusterLogin clusterLogin, IKubernetes k8s, bool details = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("Checking local container images...");

            var manifestImages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var image in KubeSetup.ClusterManifest.ContainerImages)
            {
                manifestImages.Add(image.SourceRef);
            }

            var nodes        = await k8s.CoreV1.ListNodeAsync();
            var images       = new Dictionary<string, ImageStatus>(StringComparer.InvariantCultureIgnoreCase);
            var sbImageNames = new StringBuilder();

            foreach (var node in nodes.Items)
            {
                foreach (var image in node.Status.Images)
                {
                    var found = false;

                    // $note(jefflill):
                    //
                    // Images pulled from Harbor are considered to be FOUND.  These are
                    // typically the base Kubernetes cluster images plus HAPROXY, which
                    // we use to implement etcd high-availability.

                    foreach (var name in image.Names)
                    {
                        if (name.StartsWith("registry.neon.local/") || manifestImages.Contains(name))
                        {
                            found = true;
                            break;
                        }
                    }

                    sbImageNames.Clear();

                    foreach (var name in image.Names.OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase))
                    {
                        sbImageNames.AppendWithSeparator(name, ", ");
                    }

                    var imageNames  = sbImageNames.ToString();
                    var imageStatus = new ImageStatus() { ImageNames = imageNames, NotInManifest = !found };

                    images[imageStatus.ImageNames] = imageStatus;
                }
            }

            var badImageCount = images.Values.Count(image => image.NotInManifest);

            if (badImageCount > 0 || details)
            {
                if (badImageCount > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"ERROR: [{badImageCount}] images are being pulled from external registries:");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }

                if (badImageCount > 0 || details)
                {
                    foreach (var image in images.Values.OrderBy(image => image.ImageNames))
                    {
                        var badImage = image.NotInManifest;
                        var status   = badImage ? "--> " : "    ";

                        if (badImage || details)
                        {
                            Console.WriteLine($"{status}{image.ImageNames}");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"OK: All container images are present in the cluster registry.");
            }

            return badImageCount == 0;
        }

        /// <summary>
        /// <para>
        /// Verifies that all pods running in the cluster are assigned a PriorityClass greater than
        /// or equal to <see cref="PriorityClass.NeonMin"/>, ensuring that our pods will not be evicted
        /// before user pods which could cause serious problems, especially on smalkl single node clusters.
        /// </para>
        /// <para>
        /// Details about any issues will be written to STDOUT.
        /// </para>
        /// </summary>
        /// <param name="clusterLogin">Specifies the target cluster login.</param>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <param name="details">Optionally specifies that status should be written to STDOUT when there's no errors.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        /// <remarks>
        /// <para>
        /// Verifies that all pods running in the cluster are assigned a PriorityClass greater than
        /// or equal to <see cref="PriorityClass.NeonMin"/>.  PriorityClass is used by the Kubernetes 
        /// scheduler and Kublet to decide which pods to evict when a node encounters resource pressure.  
        /// Pods with lower priority classes will tend to be evicted first.
        /// </para>
        /// <para>
        /// By default, pods will be created with <b>PriorityClass=0</b>.  Kubernetes ensures that
        /// its own critical services have very high priority class values so they will be evicted
        /// last.  neonKUBE deploys dozens of services that need to have priority classes higher
        /// than most user services.  So we assign one of several priorities to our pods based on
        /// their reliative priority as defined here: <see cref="PriorityClass"/>.
        /// </para>
        /// <para>
        /// This method is useful for ensuring that we've confiured priorites for all of our pods
        /// and also that we've done the same for pods created by third-party operators.
        /// </para>
        /// </remarks>
        public static async Task<bool> CheckPodPrioritiesAsync(ClusterLogin clusterLogin, IKubernetes k8s, bool details = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("Checking pod priorities...");

            // Build a dictionary that maps the priority of all known priority class
            // priority values to the priority class name.  Note that we're assuming
            // here that no single priority value has more than one name (we'll just
            // choose one of the names in this case) and also Build a dictionary that
            // maps all of the known priority class names to their values.

            var priorityToName = new Dictionary<int, string>();
            var nameToPriority = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var priorityClass in (await k8s.SchedulingV1.ListPriorityClassAsync()).Items)
            {
                priorityToName[priorityClass.Value]         = priorityClass.Metadata.Name;
                nameToPriority[priorityClass.Metadata.Name] = priorityClass.Value;
            }

            // Build a dictionary that maps the owner of a pod to a [PodPriorityInfo] with the
            // priority details.  The owner string indicates whether the pod owned by a daemonset,
            // stateful set, deployment, is a standalone pod, or is something else along with
            // the owner's name.

            var ownerToPriorityInfo = new Dictionary<string, PodPriorityInfo>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var @namespace in (await k8s.CoreV1.ListNamespaceAsync()).Items)
            {
                foreach (var pod in (await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Metadata.Name)).Items)
                {
                    foreach (var container in pod.Spec.Containers)
                    {
                        var ownerId = await GetOwnerIdAsync(k8s, pod);

                        ownerToPriorityInfo[ownerId] =
                            new PodPriorityInfo()
                            {
                                Owner             = ownerId,
                                Priority          = pod.Spec.Priority,
                                PriorityClassName = pod.Spec.PriorityClassName
                            };
                    }
                }
            }

            // Normalize the priority info for each pod by trying to lookup the priority from
            // the priority class name or looking up the priority class name from the priority
            // value.

            foreach (var podPriorityInfo in ownerToPriorityInfo.Values)
            {
                if (!podPriorityInfo.Priority.HasValue && !string.IsNullOrEmpty(podPriorityInfo.PriorityClassName))
                {
                    if (nameToPriority.TryGetValue(podPriorityInfo.PriorityClassName, out var foundPriority))
                    {
                        podPriorityInfo.Priority = foundPriority;
                    }
                }
                else if (podPriorityInfo.Priority.HasValue && string.IsNullOrEmpty(podPriorityInfo.PriorityClassName))
                {
                    if (priorityToName.TryGetValue(podPriorityInfo.Priority.Value, out var foundPriorityClass))
                    {
                        podPriorityInfo.PriorityClassName = foundPriorityClass;
                    }
                }

                if (!podPriorityInfo.Priority.HasValue)
                {
                    podPriorityInfo.Priority = 0;
                }

                if (string.IsNullOrEmpty(podPriorityInfo.PriorityClassName))
                {
                    podPriorityInfo.PriorityClassName = "[NONE]";
                }
            }

            var badPodDeploymentCount = ownerToPriorityInfo.Values.Count(info => info.Priority < PriorityClass.NeonMin.Value);

            if (badPodDeploymentCount > 0 || details)
            {
                if (badPodDeploymentCount > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"ERROR: [{badPodDeploymentCount}] pod deployments are deployed with [Priority<{PriorityClass.NeonMin.Value}]:");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }

                var ownerIdWidth = ownerToPriorityInfo.Keys.Max(imageName => imageName.Length);

                foreach (var item in ownerToPriorityInfo
                    .OrderByDescending(item => item.Value.Priority)
                    .ThenBy(item => item.Key))
                {
                    var priorityInfo   = item.Value;
                    var ownerFormatted = item.Key + new string(' ', ownerIdWidth - item.Key.Length);
                    var priorityValue  = priorityInfo.Priority.Value.ToString("#,##0").Trim();
                    var errorMarker    = priorityInfo.Priority.Value < PriorityClass.NeonMin.Value ? "-->" : "   ";

                    Console.WriteLine($"{errorMarker} {ownerFormatted}    - {priorityInfo.PriorityClassName} ({priorityValue})");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"OK: Pod priorities are set correctly.");
            }

            return badPodDeploymentCount > 0;
        }

        /// <summary>
        /// Returns the owner identification for a pod.
        /// </summary>
        /// <param name="k8s">The Kubernetes client.</param>
        /// <param name="pod">The pod.</param>
        /// <returns>The owner ID.</returns>
        private static async Task<string> GetOwnerIdAsync(IKubernetes k8s, V1Pod pod)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(pod != null, nameof(pod));

            // We're going to favor standard top-level owners, when present.

            string ownerName = null;
            string ownerKind = null;

            foreach (var owner in pod.OwnerReferences())
            {
                switch (owner.Kind)
                {
                    case "DaemonSet":
                    case "Deployment":
                    case "StatefulSet":

                        ownerName = owner.Name;
                        ownerKind = owner.Kind;
                        break;

                    case "Node":

                        // We'll see this for static pods.  [owner.Name] is the node name which isn't terribly useful,
                        // so we'll used the pod name instead, removing the node name part, which will look something
                        // like:
                        //
                        //      kube-scheduler-master-0

                        var nodeNamePos = pod.Metadata.Name.IndexOf(owner.Name) - 1;

                        ownerName = pod.Metadata.Name.Substring(0, nodeNamePos);
                        ownerKind = owner.Kind;
                        break;

                    case "ReplicaSet":

                        // Use the replica set's owner when present.

                        var replicaSet      = await k8s.AppsV1.ReadNamespacedReplicaSetAsync(owner.Name, pod.Namespace());
                        var replicaSetOwner = replicaSet.OwnerReferences().FirstOrDefault();

                        if (replicaSetOwner != null)
                        {
                            ownerName = replicaSetOwner.Name;
                            ownerKind = replicaSetOwner.Kind;
                        }
                        else
                        {
                            ownerName = owner.Name;
                            ownerKind = owner.Kind;
                        }

                        break;
                }
            }

            var podNamespace = pod.Namespace();

            if (!string.IsNullOrEmpty(ownerName))
            {
                return $"{podNamespace}/{ownerName} ({ownerKind})";
            }

            // Default to using the pod name or kind for standalone pods.

            return $"{podNamespace}/{pod.Name} ({pod.Kind})";
        }

        /// <summary>
        /// Verifies that all pod container specifications include resource requests and limits.
        /// </summary>
        /// <param name="clusterLogin">Specifies the target cluster login.</param>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <param name="details">Optionally specifies that status should be written to STDOUT when there's no errors.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        public static async Task<bool> CheckResourcesAsync(ClusterLogin clusterLogin, IKubernetes k8s, bool details = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterLogin != null, nameof(clusterLogin));
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("===============================================================================");
            Console.WriteLine("Checking container resources...");

            // Build a dictionary that maps the a pod reference [namespace/pod-owner-name]
            // to a list of resource information for each container in the pod.

            var podRefToContainerResources = new Dictionary<string, List<ContainerResources>>(StringComparer.InvariantCulture);

            foreach (var @namespace in (await k8s.CoreV1.ListNamespaceAsync()).Items)
            {
                foreach (var pod in (await k8s.CoreV1.ListNamespacedPodAsync(@namespace.Metadata.Name)).Items)
                {
                    var podRef     = await GetOwnerIdAsync(k8s, pod);
                    var containers = new List<ContainerResources>();

                    foreach (var containerSpec in pod.Spec.Containers)
                    {
                        var containerResources = new ContainerResources() { ContainerImage = containerSpec.Image };

                        if (containerSpec.Resources != null)
                        {
                            if (containerSpec.Resources.Requests != null)
                            {
                                containerResources.RequestCpu = GetResourceQuantity("cpu", containerSpec.Resources.Requests);
                            }

                            if (containerSpec.Resources.Requests != null)
                            {
                                containerResources.RequestMemory = GetResourceQuantity("memory", containerSpec.Resources.Requests);
                            }

                            if (containerSpec.Resources.Limits != null)
                            {
                                containerResources.LimitCpu = GetResourceQuantity("cpu", containerSpec.Resources.Requests);
                            }

                            if (containerSpec.Resources.Limits != null)
                            {
                                containerResources.LimitMemory = GetResourceQuantity("memory", containerSpec.Resources.Requests);
                            }
                        }

                        containers.Add(containerResources);
                    }

                    podRefToContainerResources[podRef] =  containers;
                }
            }

            var badPodSpecCount = podRefToContainerResources.Values.Count(containers => containers.Any(resources => resources.Error));

            if (badPodSpecCount > 0 || details)
            {
                if (badPodSpecCount > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine($"ERROR: [{badPodSpecCount}] pod deployments have containers without resource requests and/or limits.");
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                }

                foreach (var item in podRefToContainerResources
                    .OrderBy(item => item.Key))
                {
                    var containers  = item.Value;
                    var error       = containers.Any(container => container.Error);
                    var errorMarker = error ? "-->" : "   ";

                    if (error || details)
                    {
                        Console.WriteLine($"{errorMarker} {item.Key}");
                    }

                    foreach (var container in containers)
                    {
                        var requestCpu           = container.RequestCpu != null    ? container.RequestCpu.ToString()    : "NULL";
                        var requestMemory        = container.RequestMemory != null ? container.RequestMemory.ToString() : "NULL";
                        var limitCpu             = container.LimitCpu != null      ? container.LimitCpu.ToString()      : "NULL";
                        var limitMemory          = container.LimitMemory != null   ? container.LimitMemory.ToString()   : "NULL";
                        var containerError       = container.RequestCpu == null || container.RequestMemory == null || container.LimitCpu == null || container.LimitMemory == null;
                        var errorContainerMarker = containerError ? "-->" : "   ";

                        if (containerError || details)
                        {
                            Console.WriteLine($"    {errorContainerMarker} {container.ContainerImage} [request-cpu={requestCpu}] [request-memory={requestMemory}] [limit-cpu={limitCpu}] [limit-memory={limitMemory}]");
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"OK: Container resources are set correctly.");
            }

            return badPodSpecCount > 0;
        }

        /// <summary>
        /// Retrieves the named resource quantity from the resource dictionary passed.
        /// </summary>
        /// <param name="key">Identifies the resource quantity.</param>
        /// <param name="resources">The resource dictionary.</param>
        /// <returns>The <see cref="ResourceQuantity"/> of present or <c>null</c>.</returns>
        private static ResourceQuantity GetResourceQuantity(string key, IDictionary<string, ResourceQuantity> resources)
        {
            if (resources.TryGetValue(key, out var quantity))
            {
                return quantity;
            }
            else
            {
                return null;
            }
        }
    }
}
