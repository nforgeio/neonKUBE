//-----------------------------------------------------------------------------
// FILE:	    ClusterChecker.cs
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
        /// <summary>
        /// Performs development related cluster checks with information on potential
        /// problems being written to STDOUT.
        /// </summary>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        public static async Task<bool> CheckAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            var error = false;

            if (await CheckContainerImagesAsync(k8s))
            {
                error = true;
            }

            return error;
        }

        /// <summary>
        /// <para>
        /// Verifies that all of the container images running as cluster pods are specified in the
        /// container manifest.  Any images that aren't in the manifest need to be preloaded
        /// into the node image.  This is used to ensure that pods started by third-party operators
        /// are also included in the cluster manifest, ensuing tht our node images are self-contained
        /// for a better setup experience as well as air-gapped clusters.
        /// </para>
        /// <para>
        /// Details about any issues will be written to STDOUT.
        /// </para>
        /// </summary>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        public static async Task<bool> CheckContainerImagesAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("* Checking container images");

            var installedImages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var image in KubeSetup.ClusterManifest.ContainerImages)
            {
                installedImages.Add(image.SourceRef);
            }

            var nodes      = await k8s.ListNodeAsync();
            var badImages  = new List<string>();
            var sbBadImage = new StringBuilder();

            foreach (var node in nodes.Items)
            {
                foreach (var image in node.Status.Images)
                {
                    var found = false;

                    foreach (var name in image.Names)
                    {
                        if (installedImages.Contains(name))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        sbBadImage.Clear();

                        foreach (var name in image.Names.OrderBy(name => name, StringComparer.InvariantCultureIgnoreCase))
                        {
                            sbBadImage.AppendWithSeparator(name, ", ");
                        }

                        badImages.Add(sbBadImage.ToString());
                    }
                }
            }

            if (badImages.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"WARNING!");
                Console.Error.WriteLine($"========");
                Console.Error.WriteLine($"[{badImages.Count}] container images are present in cluster without being included");
                Console.Error.WriteLine($"in the cluster manifest.  These images need to be added to the node image.");
                Console.Error.WriteLine();

                foreach (var badImage in badImages)
                {
                    Console.Error.WriteLine(badImage);
                }
            }

            return badImages.Count == 0;
        }

        /// <summary>
        /// <para>
        /// Verifies that all pods running in the cluster are assigned a non-zero PriorityClass.
        /// </para>
        /// <para>
        /// Details about any issues will be written to STDOUT.
        /// </para>
        /// </summary>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <param name="displayAlways">Optionally specifies that status should be written to STDOUT when there's no errors.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        /// <remarks>
        /// <para>
        /// Verifies that all pods running in the cluster are assigned a non-zero PriorityClass.
        /// PriorityClass is used by the Kubernetes scheduler and Kublet to decide which pods
        /// to evict when a node encounters resource pressure.  Pods with lower priority classes
        /// will tend to be evicted first.
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
        public static async Task<bool> CheckPodPrioritiesAsync(IKubernetes k8s, bool displayAlways = false)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("* Checking pod priorities");

            // Build a dictionary that maps the priority of all known priority class
            // priority values to the priority class name.  Note that we're assuming
            // here that no single priority value has more than one name (we'll just
            // choose one of the names in this case).

            var priorityToName = new Dictionary<long, string>();

            foreach (var priorityClass in (await k8s.ListPriorityClassAsync()).Items)
            {
                priorityToName[priorityClass.Value] = priorityClass.Metadata.Name;
            }

            // Build a dictionary that maps container image names to the minimum pod priority
            // the image is deployed with.

            var imageToPriority = new Dictionary<string, long>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var @namespace in (await k8s.ListNamespaceAsync()).Items)
            {
                foreach (var pod in (await k8s.ListNamespacedPodAsync(@namespace.Metadata.Name)).Items)
                {
                    foreach (var container in pod.Spec.Containers)
                    {
                        var image = container.Image;

                        // $note(jefflill):
                        //
                        // It's possible, but unlikely that we could have multiple pod deployments with different
                        // priority class values.  We'll use the minimum priority in these cases.

                        if (imageToPriority.TryGetValue(image, out var priority))
                        {
                            var existingPriority = pod.Spec.Priority ?? 0;

                            imageToPriority[image] = Math.Min(priority, existingPriority);
                        }
                        else
                        {
                            imageToPriority[image] = priority;
                        }
                    }
                }
            }

            var badPodCount = imageToPriority.Values.Count(priority => priority == 0);

            if (badPodCount > 0 || displayAlways)
            {
                var writer = Console.Out;

                if (badPodCount > 0)
                {
                    writer = Console.Error;

                    writer.WriteLine();
                    writer.WriteLine($"ERROR: [{badPodCount}] images are deployed for pods with [Priority=0]:");
                    writer.WriteLine();
                }

                var imageNameWidth = imageToPriority.Keys.Max(imageName => imageName.Length);

                foreach (var item in imageToPriority.OrderByDescending(item => item.Value))
                {
                    var imageFormatted = item.Key + new string(' ', imageNameWidth - item.Key.Length);

                    if (priorityToName.TryGetValue(item.Value, out var priorityFormatted))
                    {
                        priorityFormatted = $"{priorityFormatted} ({item.Value: #,##0})";
                    }
                    else
                    {
                        priorityFormatted = $"[unknown] ({item.Value: #,##0})";
                    }

                    writer.WriteLine($"{imageFormatted}    {priorityFormatted}");
                }
            }

            return badPodCount > 0;
        }

        /// <summary>
        /// <para>
        /// Verifies that all pods running in the cluster reference local container images.
        /// </para>
        /// <para>
        /// Details about any issues will be written to STDOUT.
        /// </para>
        /// </summary>
        /// <param name="k8s">Specifies the cluster's Kubernertes client.</param>
        /// <param name="displayAlways">Optionally specifies that status should be written to STDOUT when there's no errors.</param>
        /// <returns><c>true</c> when there are no problems, <c>false</c> otherwise.</returns>
        /// <remarks>
        /// <para>
        /// neonKUBE clusters deploy all required images to CRI-O running on all cluster
        /// nodes as well as the local Harbor registry.  This not only improves the cluster
        /// setup experience but also makes air gapped cluster possible.
        /// </para>
        /// </remarks>
        public static async Task<bool> CheckPodLocalImagesAsync(IKubernetes k8s, bool displayAlways = false)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            Console.WriteLine();
            Console.WriteLine("* Checking pod local images");

            // Build list of images referenced by running pods.

            var images = new HashSet<string>();

            foreach (var @namespace in (await k8s.ListNamespaceAsync()).Items)
            {
                foreach (var pod in (await k8s.ListNamespacedPodAsync(@namespace.Metadata.Name)).Items)
                {
                    foreach (var container in pod.Spec.Containers)
                    {
                        if (!images.Contains(container.Image))
                        {
                            images.Add(container.Image);
                        }
                    }
                }
            }

            var localRegistryPrefix = $"{KubeConst.LocalClusterRegistry}/";
            var badImageCount       = images.Count(image => !image.StartsWith(localRegistryPrefix));

            if (badImageCount > 0 || displayAlways)
            {
                var writer = Console.Out;

                if (badImageCount > 0)
                {
                    writer = Console.Error;

                    writer.WriteLine();
                    writer.WriteLine($"ERROR: [{badImageCount}] images are being pulled from external registries:");
                    writer.WriteLine();
                }

                if (badImageCount > 0 || displayAlways)
                {
                    foreach (var image in images)
                    {
                        var status = image.StartsWith(localRegistryPrefix) ? "          " : "ERROR --> ";

                        writer.WriteLine($"{status}{image}");
                    }
                }
            }

            return badImageCount > 0;
        }
    }
}
