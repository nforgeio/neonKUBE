//-----------------------------------------------------------------------------
// FILE:        NodeController.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using JsonDiffPatch;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Newtonsoft.Json;

using OpenTelemetry.Trace;

using Prometheus;

// $todo(jefflill):
//
// I'm going to temporarily comment this out for the first private beta and we'll
// address this for GA.  Here's the related issue:
//
//  https://github.com/nforgeio/neonCLOUD/issues/381#issuecomment-1612082723

#if TODO

namespace NeonClusterOperator
{
    /// <summary>
    /// Monitors cluster nodes and reacts to any changes as required.
    /// </summary>
    /// <remarks>
    /// Currently, this controller looks for worker nodes with fewer than 4 vCPUs and removes them
    /// from the cluster.  This is done to block users from working around our hourly CPU fees for
    /// worker nodes for cloud deployments by first provisioning the clusterworkers with 4 vCPU VM
    /// sizes and then resizing the workers down to 2 vCPUs.  Our cloud marketplace VM images don't
    /// allow 2-vCPU VM sizes but don't charge hourly fees for VMs with only 2 vCPUs (to be more
    /// competitive with cloud integrated platforms like AKS/EKS where the entire control-plane is
    /// free.
    /// </remarks>
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    public class NodeController : IResourceController<V1Node>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NodeController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                    k8s;
        private readonly ILogger<NodeTaskController>    logger;
        private readonly TimeSpan                       minNodeRemovalAge = TimeSpan.FromHours(2);

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeController(
            IKubernetes                 k8s,
            ILogger<NodeTaskController> logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s    = k8s;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1Node node)
        {
            await SyncContext.Clear;

            await CheckNodeAsync(node);

            return ResourceControllerResult.Ok();
        }

        /// <inheritdoc/>
        public async Task StatusModifiedAsync(V1Node node)
        {
            await SyncContext.Clear;

            await CheckNodeAsync(node);
        }

        /// <summary>
        /// Checks the node passed and removes it from the cluster if it's a worker node
        /// with fewer then 4 vCPUS.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private async Task CheckNodeAsync(V1Node node)
        {
            await SyncContext.Clear;

            var nodeCreationTimestamp = node.CreationTimestamp();

            if (!nodeCreationTimestamp.HasValue)
            {
                logger.LogInformationEx(() => $"Node [{node.Name}] has not been created yet.");
                return;
            }

            // We can identify control plane nodes by checking for the existence of the
            // well-known [node-role.kubernetes.io/control-plane] label which is configured
            // by [kubeadm].

            if (!node.Metadata.Labels.ContainsKey("node-role.kubernetes.io/control-plane"))
            {
                // Don't check control-plane nodes.

                return;
            }

            // Remove worker nodes with fewer than 4 vCPUs from the cluster.

            if (!node.Status.Allocatable.TryGetValue("cpu", out var allocatableCpu))
            {
                // Looks like Kublet hasn't reported the number of CPUs yet.

                return;
            }

            var nodeCpus = allocatableCpu.ToInt32();

            if (nodeCpus < KubeConst.MinWorkerNodeVCpus)
            {
                logger.LogWarningEx(() => $"Removing worker node [{node.Name()}] because it has only [{nodeCpus}] vCPUs when at least [{KubeConst.MinWorkerNodeVCpus}] are required.");

                await k8s.CoreV1.DeleteNodeAsync(node.Name());
            }
        }
    }
}

#endif // TODO
