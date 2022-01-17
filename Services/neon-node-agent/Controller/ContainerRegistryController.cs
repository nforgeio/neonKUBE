//-----------------------------------------------------------------------------
// FILE:	    ContainerRegistryController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
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
using Neon.Retry;
using Neon.Kube.Operator;

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
    /// Manages <see cref="V1ContainerRegistry"/> resources on the Kubernetes API Server.
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

        private static readonly INeonLogger                     log             = Program.Service.LogManager.GetLogger<ContainerRegistryController>();
        private static readonly string                          configMountPath = LinuxPath.Combine(Node.HostMount, "etc/containers/registries.conf.d/00-neon-cluster.conf");
        private static ResourceManager<V1ContainerRegistry>     resourceManager;

        // Configuration settings

        private static bool         configured = false;
        private static TimeSpan     reconcileRequeueInterval;
        private static TimeSpan     errorMinRequeueInterval;
        private static TimeSpan     errorMaxRequeueInterval;

        // Metrics counters

        private static readonly Counter reconciledReceivedCounter      = Metrics.CreateCounter("containerregistry_reconciled_received", "Received ContainerRegistry reconcile events.");
        private static readonly Counter deletedReceivedCounter         = Metrics.CreateCounter("containerregistry_deleted_received", "Received ContainerRegistry deleted events.");
        private static readonly Counter statusModifiedReceivedCounter  = Metrics.CreateCounter("containerregistry_statusmodified_received", "Received ContainerRegistry status-modified events.");

        private static readonly Counter reconciledProcessedCounter     = Metrics.CreateCounter("containerregistry_reconciled_changes", "Processed ContainerRegistry reconcile events due to change.");
        private static readonly Counter deletedProcessedCounter        = Metrics.CreateCounter("containerregistry_deleted_changes", "Processed ContainerRegistry deleted events due to change.");
        private static readonly Counter statusModifiedProcessedCounter = Metrics.CreateCounter("containerregistry_statusmodified_changes", "Processed ContainerRegistry status-modified events due to change.");

        private static readonly Counter reconciledErrorCounter         = Metrics.CreateCounter("containerregistry_reconciled_error", "Failed ContainerRegistry reconcile event processing.");
        private static readonly Counter deletedErrorCounter            = Metrics.CreateCounter("containerregistry_deleted_error", "Failed ContainerRegistry deleted event processing.");
        private static readonly Counter statusModifiedErrorCounter     = Metrics.CreateCounter("containerregistry_statusmodified_error", "Failed ContainerRegistry status-modified events processing.");

        private static readonly Counter configUpdateCounter            = Metrics.CreateCounter("containerregistry_node_updated", "Number of node config updates.");
        private static readonly Counter loginErrorCounter              = Metrics.CreateCounter("containerregistry_login_error", "Number of failed container registry logins.");

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Coinstructor.
        /// </summary>
        public ContainerRegistryController()
        {
            // Load the configuration settings the first time a controller instance is created.

            if (!configured)
            {
                reconcileRequeueInterval = Program.Service.Environment.Get("CONTAINERREGISTRY_RECONCILE_REQUEUE_INTERVAL", TimeSpan.FromMinutes(5));
                errorMinRequeueInterval  = Program.Service.Environment.Get("CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL", TimeSpan.FromSeconds(15));
                errorMaxRequeueInterval  = Program.Service.Environment.Get("CONTAINERREGISTRY_ERROR_MAX_REQUEUE_INTERVAL", TimeSpan.FromMinutes(10));

                resourceManager = new ResourceManager<V1ContainerRegistry>()
                {
                    ReconcileRequeueInterval = reconcileRequeueInterval,
                    ErrorMinRequeueInterval  = errorMinRequeueInterval,
                    ErrorMaxRequeueInterval  = errorMaxRequeueInterval
                };

                configured = true;
            }
        }

        /// <summary>
        /// Called for each existing custom resource when the controller starts so that the controller
        /// can maintain the status of all resources and then afterwards, this will be called whenever
        /// a resource is added or has a non-status update.
        /// </summary>
        /// <param name="registry">The new entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1ContainerRegistry registry)
        {
            reconciledReceivedCounter.Inc();

            await resourceManager.ReconciledAsync(registry,
                async (name, resources) =>
                {
                    log.LogInfo($"RECONCILED: {name ?? "[NO-CHANGE]"}");
                    reconciledProcessedCounter.Inc();

                    UpdateContainerRegistries(resources);

                    return await Task.FromResult<ResourceControllerResult>(null);
                },
                errorCounter: reconciledErrorCounter);

            return ResourceControllerResult.RequeueEvent(errorMinRequeueInterval);
        }

        /// <summary>
        /// Called when a custom resource is removed from the API Server.
        /// </summary>
        /// <param name="registry">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1ContainerRegistry registry)
        {
            deletedReceivedCounter.Inc();

            await resourceManager.DeletedAsync(registry,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");
                    deletedProcessedCounter.Inc();

                    UpdateContainerRegistries(resources);

                    return await Task.FromResult<ResourceControllerResult>(null);
                },
                errorCounter: deletedErrorCounter);
        }

        /// <summary>
        /// Called when a custom resource's status has been modified.
        /// </summary>
        /// <param name="registry">The updated entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> StatusModifiedAsync(V1ContainerRegistry registry)
        {
            statusModifiedReceivedCounter.Inc();

            await resourceManager.DeletedAsync(registry,
                async (name, resources) =>
                {
                    log.LogInfo($"DELETED: {name}");
                    statusModifiedProcessedCounter.Inc();

                    UpdateContainerRegistries(resources);

                    return await Task.FromResult<ResourceControllerResult>(null);
                },
                errorCounter: statusModifiedErrorCounter);

            return ResourceControllerResult.RequeueEvent(errorMinRequeueInterval);
        }

        /// <summary>
        /// Rebuilds the host node's <b>/etc/containers/registries.conf.d/00-neon-cluster.conf</b> file,
        /// using the container registries passed and then signals CRI-O to reload any changes.
        /// </summary>
        /// <param name="registries">The current registry configurations.</param>
        private async void UpdateContainerRegistries(IReadOnlyDictionary<string, V1ContainerRegistry> registries)
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
                Node.ExecuteCapture("/usr/bin/pkill", new object[] { "-HUP", "crio" }).EnsureSuccess();

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
                        Node.ExecuteCapture("podman", "logout", registry.Spec.Location);
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

                        retry.Invoke(
                            () =>
                            {
                                log.LogInfo($"podman login {registry.Spec.Location} --username {registry.Spec.Username} --password REDACTED");
                                Node.ExecuteCapture("podman", "login", registry.Spec.Location, "--username", registry.Spec.Username, "--password", registry.Spec.Password).EnsureSuccess();
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

            foreach (var location in existingLocations)
            {
                if (!registries.Values.Any(registry => location == registry.Spec.Location))
                {
                    log.LogInfo($"podman logout {location}");
                    Node.ExecuteCapture("podman", "logout", location);
                }
            }
        }
    }
}
