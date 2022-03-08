//-----------------------------------------------------------------------------
// FILE:	    PodMutationController.cs
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
using KubeOps.Operator.Webhooks;

using Prometheus;

namespace NeonNodeAgent
{
#if TODO
    /// <summary>
    /// Implements a mutating admission webhook that watches for new <see cref="V1Pod"/> instances
    /// in the cluster to perform any necessary modifications before the pod is created or updated.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Currently, this controller is only being used to ensure that the <b>Harbor</b> related components
    /// running in the <b>neon-system</b> namespace have <b>priority class name</b> assignments because
    /// the operator we're using to manage Harbor doesn't have a way to specify eviction priorities.
    /// </para>
    /// </remarks>
    public class PodMutationController : IMutationWebhook<V1Pod>
    {
        /// <summary>
        /// Specifies which operations to hook.
        /// </summary>
        public AdmissionOperations Operations => AdmissionOperations.Create | AdmissionOperations.Update;

        /// <summary>
        /// Called when a new pod is created.
        /// </summary>
        /// <param name="newPod">The new pod.</param>
        /// <param name="dryRun">Indicates whether this is a dry run.</param>
        /// <returns>The mutation result.</returns>
        public async Task<MutationResult> CreateAsync(V1Pod newPod, bool dryRun)
        {
            return await Task.FromResult(MutationResult.NoChanges());
        }

        /// <summary>
        /// Called when a pod is updated.
        /// </summary>
        /// <param name="originalPod">The original pod.</param>
        /// <param name="newPod">The new pod.</param>
        /// <param name="dryRun">Indicates whether this is a dry run.</param>
        /// <returns>The mutation result.</returns>
        public async Task<MutationResult> UpdateAsync(V1Pod originalPod, V1Pod newPod, bool dryRun)
        {
            await SyncContext.ClearAsync;

            return await Task.FromResult(MutationResult.NoChanges());
        }
    }
#endif // TODO
}
