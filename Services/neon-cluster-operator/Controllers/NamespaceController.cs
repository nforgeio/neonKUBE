//-----------------------------------------------------------------------------
// FILE:	    NamespaceController.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.Extensions.Logging;

using JsonDiffPatch;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Autorest;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz.Impl;
using Quartz;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Removes <see cref="V1Namespace"/> resources assigned to nodes that don't exist.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller relies on a lease named <b>neon-cluster-operator.namespace</b>.  
    /// This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace
    /// and will be used to a leader to manage these resources.
    /// </para>
    /// <para>
    /// The <b>neon-cluster-operator</b> won't conflict with node agents because we're only 
    /// removing tasks that don't belong to an existing node.
    /// </para>
    /// </remarks>
    [EntityRbac(typeof(V1Namespace), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NamespaceController : IOperatorController<V1Namespace>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NamespaceController>();

        private static ResourceManager<V1Namespace, NamespaceController> resourceManager;

        private static IScheduler Scheduler;
        private static StdSchedulerFactory SchedulerFactory;
        private static bool Initialized;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NamespaceController()
        {
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(IKubernetes k8s)
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("start", attributes => attributes.Add("resource", nameof(V1Namespace)));

                Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

                // Load the configuration settings.

                var leaderConfig =
                    new LeaderElectionConfig(
                        k8s,
                        @namespace: KubeNamespace.NeonSystem,
                        leaseName: $"{Program.Service.Name}.namespace",
                        identity: Pod.Name,
                        promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_promoted", "Leader promotions"),
                        demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_demoted", "Leader demotions"),
                        newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_newLeader", "Leadership changes"));

                var options = new ResourceManagerOptions()
                {
                    IdleCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_idle", "IDLE events processed."),
                    ReconcileCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_idle", "RECONCILE events processed."),
                    DeleteCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_idle", "DELETED events processed."),
                    StatusModifyCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_idle", "STATUS-MODIFY events processed."),
                    IdleErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_idle_error", "Failed ClusterOperatorSettings IDLE event processing."),
                    ReconcileErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_reconcile_error", "Failed ClusterOperatorSettings RECONCILE event processing."),
                    DeleteErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_delete_error", "Failed ClusterOperatorSettings DELETE event processing."),
                    StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}namespace_statusmodify_error", "Failed ClusterOperatorSettings STATUS-MODIFY events processing.")
                };

                resourceManager = new ResourceManager<V1Namespace, NamespaceController>(
                    k8s,
                    options: options,
                    leaderConfig: leaderConfig);

                await resourceManager.StartAsync();

                SchedulerFactory = new StdSchedulerFactory();
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NamespaceController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));

            this.k8s = k8s;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("idle", attributes => attributes.Add("resource", nameof(V1Namespace)));
                log.LogInformationEx("[IDLE]");
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1Namespace resource)
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("resource", nameof(V1Namespace)));

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

                if (resource.Metadata.Annotations.TryGetValue(NeonAnnotation.OtelCollector, out var otel))
                {
                    if (otel.ToLower() == "disabled")
                    {
                        await RemoveServiceAsync(resource.Name());
                        log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");
                    }
                }

                try
                {
                    await k8s.ReadNamespacedServiceAsync(NeonHelper.NeonKubeOtelCollectorName, resource.Name());
                }
                catch
                {
                    await AddServiceAsync(resource.Name());
                }

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1Namespace resource)
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("delete", attributes => attributes.Add("resource", nameof(V1Namespace)));

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return;
                }

                log.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("promotion", attributes => attributes.Add("resource", nameof(V1Namespace)));

                log.LogInformationEx(() => $"PROMOTED");
            }
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("promotion", attributes => attributes.Add("resource", nameof(V1Namespace)));

                log.LogInformationEx(() => $"DEMOTED");
            }
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            using (Tracer.CurrentSpan)
            {
                Tracer.CurrentSpan?.AddEvent("promotion", attributes => 
                {
                    attributes.Add("leader", identity);
                    attributes.Add("resource", nameof(V1Namespace));
                });

                log.LogInformationEx(() => $"NEW LEADER: {identity}");

                await SyncContext.Clear;
            }
        }

        private async Task AddServiceAsync(string @namespace)
        {
            var service = new V1Service()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = NeonHelper.NeonKubeOtelCollectorName,
                    NamespaceProperty = @namespace,
                    Labels = new Dictionary<string, string>
                    {

                    }
                },
                Spec = new V1ServiceSpec()
                {
                    Ports = new V1ServicePort[]
                    {
                        new V1ServicePort()
                        {
                            Name = "agent-otlp-grpc",
                            Protocol = "TCP",
                            Port = 80,
                            TargetPort = 4317
                        }
                    }
                }
            };

            await k8s.CreateNamespacedServiceAsync(service, service.Namespace());

            var virtualService = new VirtualService()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name              = service.Name(),
                    NamespaceProperty = service.Namespace()
                },
                Spec = new VirtualServiceSpec()
                {
                    Http = new List<HTTPRoute>()
                    {
                        new HTTPRoute()
                        {
                            Route = new List<HTTPRouteDestination>()
                            {
                                new HTTPRouteDestination()
                                {
                                    Destination = new Destination()
                                    {
                                        Host = "grafana-agent-node.neon-monitor.svc.cluster.local",
                                        Port = new PortSelector()
                                        {
                                            Number = 4317
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            await k8s.CreateNamespacedCustomObjectAsync<VirtualService>(virtualService, virtualService.Name(), virtualService.Namespace());
        }

        private async Task RemoveServiceAsync(string @namespace)
        {
            try
            {
                await k8s.DeleteNamespacedServiceAsync(NeonHelper.NeonKubeOtelCollectorName, @namespace);
            }
            catch
            {
                // doesn't exist
            }
        }
    }
}
