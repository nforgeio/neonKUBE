//-----------------------------------------------------------------------------
// FILE:	    ContainerRegistryController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;

using k8s;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Prometheus;
using Tomlyn;

namespace NeonNodeAgent
{
    /// <summary>
    /// <para>
    /// Manages <see cref="V1ContainerRegistry"/> resources on the Kubernetes API Server.
    /// </para>
    /// <note>
    /// This controller relies on a lease named like <b>neon-node-agent.containerregistry-NODENAME</b>
    /// where <b>NODENAME</b> is the name of the node where the <b>neon-node-agent</b> operator
    /// is running.  This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> 
    /// namespace and will be used to elect a leader for the node in case there happens to be two
    /// agents running on the same node for some reason.
    /// </note>
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
    [EntityRbac(typeof(V1ContainerRegistry), Verbs = RbacVerb.Get | RbacVerb.Patch | RbacVerb.List | RbacVerb.Watch | RbacVerb.Update)]
    public class ContainerRegistryController : IResourceController<V1ContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger log             = Program.Service.LogManager.GetLogger<ContainerRegistryController>();
        private static readonly string      configMountPath = LinuxPath.Combine(Node.HostMount, "etc/containers/registries.conf.d/00-neon-cluster.conf");

        private static ResourceManager<V1ContainerRegistry, ContainerRegistryController> resourceManager;

        // Metrics counters

        private static readonly Counter configUpdateCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_node_updated", "Number of node config updates.");
        private static readonly Counter loginErrorCounter   = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_login_error", "Number of failed container registry logins.");

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Load the configuration settings.

            var leaderConfig = 
                new LeaderElectionConfig(
                    k8s,
                    @namespace:       KubeNamespace.NeonSystem,
                    leaseName:        $"{Program.Service.Name}.containerregistry-{Node.Name}",
                    identity:         Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_promoted", "Leader promotions"),
                    demotionCounter:  Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                Mode                       = ResourceManagerMode.Normal,
                IdleInterval               = Program.Service.Environment.Get("CONTAINERREGISTRY_IDLE_INTERVAL", TimeSpan.FromMinutes(5)),
                ErrorMinRequeueInterval    = Program.Service.Environment.Get("CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15)),
                ErrorMaxRetryInterval      = Program.Service.Environment.Get("CONTAINERREGISTRY_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromSeconds(60)),
                ReconcileCounter           = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_reconciled_changes", "Processed ContainerRegistry reconcile events due to change."),
                DeleteCounter              = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_deleted_received", "Received ContainerRegistry deleted events."),
                StatusModifiedCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_statusmodified_received", "Received ContainerRegistry status-modified events."),
                ReconcileErrorCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_reconciled_error", "Failed NodeTask reconcile event processing."),
                DeleteErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_deleted_error", "Failed NodeTask deleted event processing."),
                StatusModifiedErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistry_statusmodified_error", "Failed NodeTask status-modified events processing.")
            };

            resourceManager = new ResourceManager<V1ContainerRegistry, ContainerRegistryController>(
                k8s,
                options:      options,
                leaderConfig: leaderConfig);

            await resourceManager.StartAsync();
        }
        
        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ContainerRegistryController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));

            this.k8s = k8s;
        }

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="registry">The new entity or <c>null</c> when nothing has changed.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1ContainerRegistry registry)
        {
            await resourceManager.ReconciledAsync(registry,
                async (resource, resources) =>
                {
                    var name = resource?.Name();

                    log.LogInfo($"RECONCILED: {name ?? "[IDLE]"}");
                    await UpdateContainerRegistriesAsync(resources);

                    return null;
                });

            return null;
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="registry">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1ContainerRegistry registry)
        {
            await resourceManager.DeletedAsync(registry,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");

                    await UpdateContainerRegistriesAsync(resources);
                });
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="registry">The updated entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StatusModifiedAsync(V1ContainerRegistry registry)
        {
            await resourceManager.StatusModifiedAsync(registry,
                async (name, resources) =>
                {
                    // This is a NO-OP

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Rebuilds the host node's <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file,
        /// using the container registries passed and then signals CRI-O to reload any changes.
        /// </summary>
        /// <param name="registries">The current registry configurations.</param>
        private async Task UpdateContainerRegistriesAsync(IReadOnlyDictionary<string, V1ContainerRegistry> registries)
        {
            // NOTE: Here's the documentation for the config file we're generating:
            //
            //      https://github.com/containers/image/blob/main/docs/containers-registries.conf.5.md
            //

            var sbRegistryConfig   = new StringBuilder();
            var sbSearchRegistries = new StringBuilder();

            // Configure any unqualified search registries.

            foreach (var registry in registries.Values
                .Where(registry => registry.Spec.SearchOrder >= 0)
                .OrderBy(registry => registry.Spec.SearchOrder))
            {
                sbSearchRegistries.AppendWithSeparator($"\"{registry.Spec.Prefix}\"", ", ");
            }

            sbRegistryConfig.Append(
$@"unqualified-search-registries = [{sbSearchRegistries}]
");

            // Configure any container registries include the local cluster.

            foreach (var registry in registries.Values)
            {
                sbRegistryConfig.Append(
$@"
[[registry]]
prefix   = ""{registry.Spec.Prefix}""
insecure = {NeonHelper.ToBoolString(registry.Spec.Insecure)}
blocked  = {NeonHelper.ToBoolString(registry.Spec.Blocked)}
");

                if (!string.IsNullOrEmpty(registry.Spec.Location))
                {
                    sbRegistryConfig.AppendLine($"location = \"{registry.Spec.Location}\"");
                }
            }

            // Read and parse the current configuration file to create list of the existing
            // configured upstream registries.  We'll need this so we can logout any registries
            // that are being deleted.

            var currentConfigText  = File.ReadAllText(configMountPath);
            var currentConfig      = Toml.Parse(currentConfigText);
            var existingLocations  = new List<string>();

            foreach (var registryTable in currentConfig.Tables.Where(table => table.Name.Key.GetName() == "registry"))
            {
                var location = registryTable.Items.SingleOrDefault(key => key.Key.GetName() == "location")?.Value.GetValue();

                if (!string.IsNullOrWhiteSpace(location))
                {
                    existingLocations.Add(location);
                }
            }

            // Convert the generated config to Linux line endings and then compare the new
            // config against what's already configured on the host node.  We'll rewrite the
            // host file and then signal CRI-O to reload its config when the files differ.

            var newConfigText = NeonHelper.ToLinuxLineEndings(sbRegistryConfig.ToString());

            if (currentConfigText != newConfigText)
            {
                configUpdateCounter.Inc();

                File.WriteAllText(configMountPath, newConfigText);
                (await Node.ExecuteCaptureAsync("pkill", new object[] { "-HUP", "crio" })).EnsureSuccess();

                // Wait a few seconds to give CRI-O a chance to reload its config.  This will
                // help mitigate problems when managing logins below due to potential inconsistencies
                // between CRI-O's currently loaded config and the new config we just saved.

                await Task.Delay(TimeSpan.FromSeconds(15));
            }

            // We also need to log into each of the registries that require credentials
            // via [podman] on the node.  We also need to logout of registries that don't
            // specify credentials to handle cases where the registry was originally
            // logged in, but has no credentials now.
            //
            // We'll log individual login failures but will continue trying to log into any
            // remaining registries.

            var retry = new LinearRetryPolicy(e => true, maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

            foreach (var registry in registries.Values)
            {
                try
                {
                    if (string.IsNullOrEmpty(registry.Spec.Username))
                    {
                        // The registry doesn't have a username so we'll logout to clear any old credentials.
                        // We're going to ignore any errors here in case we're not currently logged into
                        // the registry.

                        log.LogInfo($"podman logout {registry.Spec.Location}");
                        await Node.ExecuteCaptureAsync("podman", new object[] { "logout", registry.Spec.Location });
                    }
                    else
                    {
                        // The registry has credentials so login using them.

                        // $note(jefflill):
                        //
                        // It's possible that CRI-O hasn't reloaded its config yet and we may see errors
                        // when logging into a new registry we just configured that CRI-O isn't aware of
                        // yet.  The delay above should mitigate this most of the time, so we're going
                        // to retry here, just in case.

                        await retry.InvokeAsync(
                            async () =>
                            {
                                log.LogInfo($"podman login {registry.Spec.Location} --username {registry.Spec.Username} --password REDACTED");
                                (await Node.ExecuteCaptureAsync("podman", new object[] { "login", registry.Spec.Location, "--username", registry.Spec.Username, "--password", registry.Spec.Password })).EnsureSuccess();
                            });
                    }
                }
                catch (Exception e)
                {
                    loginErrorCounter.Inc();
                    log.LogError(e);
                }
            }

            // We also need to log out of registeries that were just removed from the configuration.
            // We're going to ignore any errors.

            foreach (var location in existingLocations)
            {
                if (!registries.Values.Any(registry => location == registry.Spec.Location))
                {
                    log.LogInfo($"podman logout {location}");
                    await Node.ExecuteCaptureAsync("podman", new object[] { "logout", location });
                }
            }
        }
    }
}
