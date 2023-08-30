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
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Operator.ResourceManager;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Newtonsoft.Json;

using OpenTelemetry.Trace;

using Prometheus;
using Neon.Operator.Attributes;

// $todo(jefflill):
//
// This needs to be converted into a cron job
//
//      https://github.com/nforgeio/neonKUBE/issues/1858

#if TODO

namespace NeonClusterOperator
{
    /// <summary>
    /// Monitors cluster nodes and reacts to any changes as required.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently, this controller looks for worker nodes for cloud deployed clusters with fewer than
    /// 4 vCPUs and then removes them from the cluster.  This is done to block users from working around
    /// our hourly CPU fees for worker nodes for cloud deployments by first provisioning the clusterworkers
    /// with 4 vCPU VM sizes and then resizing the workers down to 2 vCPUs.  Our cloud marketplace VM
    /// images allow 2-vCPU VM sizes so we can provision control-plane VMs with 2 vCPUs (to be more
    /// competitive with cloud integrated platforms like AKS/EKS where the entire control-plane is free.
    /// </para>
    /// <note>
    /// This controller removes these nodes from the cluster but does not remove the host VMs from the
    /// cloud so the user can have a chance to rejoin these VMs to the cluster or otherwise recover data.
    /// </note>
    /// </remarks>
    [RbacRule<V1Node>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    public class NodeController : ResourceControllerBase<V1Node>
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
        private readonly TimeSpan                       requeueDelay;
        private ClusterInfo                             clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeController(
            IKubernetes                 k8s,
            ILogger<NodeTaskController> logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s          = k8s;
            this.logger       = logger;
            this.requeueDelay = TimeSpan.FromSeconds(60);
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1Node node)
        {
            await SyncContext.Clear;

            // We're going to fetch the cluster info configmap so we can use its
            // [HostingEnvironment] property to determine whether the cluster is
            // hosted in a cloud.

            // $note(jefflill):
            //
            // In theory, users could defeat the 2-vCPU check by manually editing
            // the cluster info configmap hosting environment to an on-premise
            // alternative.  This would break future cloud features like scaling
            // and perhaps repair, so I'm not going to worry about this now.

            if (clusterInfo == null)
            {
                clusterInfo = (await k8s.CoreV1.ReadNamespacedTypedConfigMapAsync<ClusterInfo>(KubeConfigMapName.ClusterInfo, KubeNamespace.NeonStatus)).Data;
            }

            if (KubeHelper.IsPaidHostingEnvironment(clusterInfo.HostingEnvironment) || true)    // $debug(jefflill): REMOVE THE TRUE
            {
                // Perform this check only for hosting environments where NEONFORGE collects revenue.

                return await CheckNodeAsync(node);
            }

            return ResourceControllerResult.Ok();
        }

        /// <summary>
        /// Checks the node passed and removes it from the cluster if the cluster is running
        /// in a cloud and the worker node has fewer than 4 vCPUS.
        /// </summary>
        /// <param name="node"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task<ResourceControllerResult> CheckNodeAsync(V1Node node)
        {
            await SyncContext.Clear;

            var nodeCreationTimestamp = node.CreationTimestamp();

            if (!nodeCreationTimestamp.HasValue)
            {
                logger.LogInformationEx(() => $"Node [{node.Name}] has not been created yet.");

                return ResourceControllerResult.RequeueEvent(requeueDelay);
            }

            // We can identify control plane nodes by checking for the existence of the
            // well-known [node-role.kubernetes.io/control-plane] label which is configured
            // by [kubeadm].

            if (node.Metadata.Labels.ContainsKey("node-role.kubernetes.io/control-plane"))
            {
                // Don't check control-plane nodes.

                return ResourceControllerResult.Ok();
            }

            // Remove worker nodes with fewer than 4 vCPUs from the cluster.

            if (!node.Status.Allocatable.TryGetValue("cpu", out var allocatableCpu))
            {
                // Looks like Kublet hasn't reported the number of CPUs yet.

                return ResourceControllerResult.RequeueEvent(requeueDelay);
            }

            var vCpus = allocatableCpu.ToInt32();

            if (vCpus < KubeConst.MinWorkerNodeVCpus)
            {
                logger.LogCriticalEx(() => $"Removing worker node [{node.Name()}] because it has only [{vCpus}] vCPUs when at least [{KubeConst.MinWorkerNodeVCpus}] are required.");
                await k8s.CoreV1.DeleteNodeAsync(node.Name());
            }

            return ResourceControllerResult.Ok();
        }
    }
}

#endif
