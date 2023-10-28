//-----------------------------------------------------------------------------
// FILE:        NodeTaskController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Tasks;

using OpenTelemetry.Trace;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Removes <see cref="V1NeonNodeTask"/> resources assigned to nodes that don't exist.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This controller relies on a lease named <b>neon-cluster-operator.nodetask</b>.  
    /// This lease will be persisted in the <see cref="KubeNamespace.NeonSystem"/> namespace
    /// and will be used to a leader to manage these resources.
    /// </para>
    /// <para>
    /// The <b>neon-cluster-operator</b> won't conflict with node agents because we're only 
    /// removing tasks that don't belong to an existing node.
    /// </para>
    /// </remarks>
    [RbacRule<V1NeonNodeTask>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NodeTaskController : ResourceControllerBase<V1NeonNodeTask>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeTaskController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                    k8s;
        private readonly ILogger<NodeTaskController>    logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeTaskController(
            IKubernetes                 k8s,
            ILogger<NodeTaskController> logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s    = k8s;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override async Task StatusModifiedAsync(V1NeonNodeTask resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("status-modified", attributes => attributes.Add("customresource", nameof(V1NeonNodeTask)));

                if (resource.Metadata.Labels.TryGetValue(NeonLabel.NodeTaskType, out var nodeTaskType))
                {
                    switch (nodeTaskType) 
                    {
                        case NeonNodeTaskType.ControlPlaneCertExpirationCheck:

                            if (resource.Status.Phase == V1NeonNodeTask.Phase.Success)
                            {
                                await ProcessControlPlaneCertCheckAsync(resource);
                            }
                            break;

                        default:

                            break;
                    }
                }
            }
        }

        private async Task ProcessControlPlaneCertCheckAsync(V1NeonNodeTask resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Dictionary<string, int> expirations = new Dictionary<string, int>();

                var reading = false;

                foreach (var line in resource.Status.Output.ToLines())
                {
                    if (reading && line.StartsWith("CERTIFICATE AUTHORITY") || line.IsEmpty())
                    {
                        reading = false;
                        continue;
                    }

                    if (line.StartsWith("CERTIFICATE"))
                    {
                        reading = true;
                        continue;
                    }

                    if (!reading)
                    {
                        continue;
                    }

                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    expirations.Add(parts[0], int.Parse(parts[6].TrimEnd('d')));
                }

                if (expirations.Any(exp => exp.Value < 90))
                {
                    using (var nodeTaskActivity = TelemetryHub.ActivitySource?.StartActivity("CreateUpdateCertNodeTask"))
                    {
                        var nodeTask = new V1NeonNodeTask()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name   = $"control-plane-cert-update-{NeonHelper.CreateBase36Uuid()}",
                                Labels = new Dictionary<string, string>
                                {
                                    { NeonLabel.ManagedBy, KubeService.NeonClusterOperator },
                                    { NeonLabel.NodeTaskType, NeonNodeTaskType.ControlPlaneCertUpdate }
                                }
                                },
                                Spec = new V1NeonNodeTask.TaskSpec()
                                {
                                    Node                = resource.Spec.Node,
                                    StartAfterTimestamp = DateTime.UtcNow,
                                    BashScript          = @"
set -e

/usr/bin/kubeadm certs renew all

for pod in `crictl pods | tr -s ' ' | cut -d "" "" -f 1-6 | grep kube | cut -d "" "" -f1`;
do 
    crictl stopp $pod;
    crictl rmp $pod;
done
",
                                    CaptureOutput = true,
                                    RetentionSeconds = TimeSpan.FromDays(1).Seconds
                                }
                            };

                        var tasks = await k8s.CustomObjects.ListClusterCustomObjectAsync<V1NeonNodeTask>(labelSelector: $"{NeonLabel.NodeTaskType}={NeonNodeTaskType.ControlPlaneCertUpdate}");

                        if (!tasks.Items.Any(task => task.Spec.Node == nodeTask.Spec.Node && (task.Status.Phase <= V1NeonNodeTask.Phase.Running || task.Status == null)))
                        {
                            await k8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonNodeTask>(nodeTask, name: nodeTask.Name());
                        }
                    }
                }
            }
        }
    }
}
