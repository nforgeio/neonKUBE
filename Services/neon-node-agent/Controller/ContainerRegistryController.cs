//-----------------------------------------------------------------------------
// FILE:	    ContainerRegistryController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Operator;

using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;
using System.Collections.Generic;

namespace NeonNodeAgent
{
    /// <summary>
    /// Manages <see cref="V1ContainerRegistry"/> entities on the Kubernetes API Server.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This operator controller is responsible for managing the upstream CRI-O container registry
    /// configuration located at <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b>.
    /// on the host node.
    /// </para>
    /// <note>
    /// The host node file system is mounted into the container at: <see cref="Program.HostMount"/>.
    /// </note>
    /// <para>
    /// This works by monitoring by:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Monitoring the <see cref="V1ContainerRegistry"/> resources for potential changes
    /// and then performing the steps below a change is detected.
    /// </item>
    /// <item>
    /// Regenerate the contents of the <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file.
    /// </item>
    /// <item>
    /// Compare the contents of the current file with the new generated config.
    /// </item>
    /// <item>
    /// If the contents differ, update the file on the host's filesystem and then signal
    /// CRI-O to reload its configuration.
    /// </item>
    /// </list>
    /// </remarks>
    [EntityRbac(typeof(V1ContainerRegistry), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Watch | RbacVerb.Update)]
    public class ContainerRegistryController : IResourceController<V1ContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger                             log             = LogManager.Default.GetLogger<ContainerRegistryController>();
        private static readonly ResourceManager<V1ContainerRegistry>    resourceManager = new ResourceManager<V1ContainerRegistry>();
        private static readonly string                                  configMountPath = LinuxPath.Combine(Program.HostMount, "etc/containers/registries.conf.d/00-neon-cluster.conf");

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="resource">The new entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1ContainerRegistry resource)
        {
            if (resourceManager.Reconciled(resource, resources => UpdateContainerRegistries(resources)))
            {
                log.LogInfo($"RECONCILE: {resource.Name()}");

                await Task.CompletedTask;
            }

            return null;
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="resource">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1ContainerRegistry resource)
        {
            if (resourceManager.Deleted(resource, resources => UpdateContainerRegistries(resources)))
            {
                log.LogInfo($"DELETED: {resource.Name()}");

                await Task.CompletedTask;
            }
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="resource">The updated entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> StatusModifiedAsync(V1ContainerRegistry resource)
        {
            if (resourceManager.StatusModified(resource))
            {
                log.LogInfo($"MODIFIED: {resource.Name()}");

                await Task.CompletedTask;
            }

            return null;
        }

        /// <summary>
        /// Rebuilds the host node's <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file,
        /// using the container registries passed and then signals CRI-O to reload any changes.
        /// </summary>
        /// <param name="registries">The current registry configurations.</param>
        private void UpdateContainerRegistries(IEnumerable<V1ContainerRegistry> registries)
        {
            var sbRegistryConfig   = new StringBuilder();
            var searchRegistries   = registries.Where(registry => registry.Spec.SearchOrder > 0);
            var sbSearchRegistries = new StringBuilder();

            // Specify any unqualified search registries.

            foreach (var registry in searchRegistries.OrderBy(registry => registry.Spec.SearchOrder))
            {
                sbSearchRegistries.AppendWithSeparator($"\"{registry.Spec.Prefix}\"", ", ");
            }

            sbRegistryConfig.Append(
$@"unqualified-search-registries = [{sbSearchRegistries}]
");

            // Specify the built-in cluster registry.

            sbRegistryConfig.Append(
$@"
[[registry]]
prefix   = ""{KubeConst.LocalClusterRegistry}""
insecure = true
blocked  = false
location = ""{KubeConst.LocalClusterRegistry}""
");

            // Specify any custom upstream registries.

            foreach (var registry in registries)
            {
                sbRegistryConfig.Append(
$@"
[[registry]]
prefix   = ""{registry.Spec.Prefix}""
insecure = {NeonHelper.ToBoolString(registry.Spec.Insecure)}
blocked  = {NeonHelper.ToBoolString(registry.Spec.Blocked)}
location = ""{registry.Spec.Prefix}""
");

                if (!string.IsNullOrEmpty(registry.Spec.Location))
                {
                    sbRegistryConfig.AppendLine($"location = \"{registry.Spec.Location}\"");
                }
            }

            // Convert the generated config to Linux line endings and then compare the new
            // config against what's already configured on the host node.  We'll rewrite the
            // host file and then signal CRI-O to reload its config when the files differ.

            var newConfig = NeonHelper.ToLinuxLineEndings(sbRegistryConfig.ToString());

            if (File.ReadAllText(configMountPath) != newConfig)
            {
                File.WriteAllText(configMountPath, newConfig);
                Program.HostExecuteCapture("/usr/bin/pkill", new object[] { "-HUP", "crio" }).EnsureSuccess();
            }
        }
    }
}
