//-----------------------------------------------------------------------------
// FILE:        NeonDashboardController.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Tasks;

using OpenTelemetry.Trace;

using Task = System.Threading.Tasks.Task;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages <see cref="V1NeonDashboard"/> resources.
    /// </summary>
    [RbacRule<V1NeonDashboard>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [ResourceController(MaxConcurrentReconciles = 5)]
    public class NeonDashboardController : ResourceControllerBase<V1NeonDashboard>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonDashboardController() { }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                        k8s;
        private readonly ILogger<NeonDashboardController>   logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonDashboardController(
            IKubernetes k8s,
            ILogger<NeonDashboardController> logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s    = k8s;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonDashboard resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonDashboard)));
                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonDashboard resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }
    }
}
