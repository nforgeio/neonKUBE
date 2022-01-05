//-----------------------------------------------------------------------------
// FILE:	    ContainerRegistryController.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Resources;

using k8s.Models;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

namespace NeonClusterOperator
{
    /// <summary>
    /// Manages <see cref="V1ContainerRegistry"/> entities on the Kubernetes API Server.
    /// </summary>
    [EntityRbac(typeof(V1ContainerRegistry), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Watch | RbacVerb.Update)]
    public class ContainerRegistryController : IResourceController<V1ContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly INeonLogger log = LogManager.Default.GetLogger<ContainerRegistryController>();

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Called when an entity is added or has seen a non-status update.
        /// </summary>
        /// <param name="entity">The new entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> ReconcileAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(ReconcileAsync)}.");

            await Task.CompletedTask;

            return null;
        }

        /// <summary>
        /// Called when an entity's status has been modified.
        /// </summary>
        /// <param name="entity">The updated entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> StatusModifiedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(StatusModifiedAsync)}.");

            await Task.CompletedTask;

            return null;
        }

        /// <summary>
        /// Called when an entity is removed from the API Server.
        /// </summary>
        /// <param name="entity">The deleted entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DeletedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(DeletedAsync)}.");

            await Task.CompletedTask;
        }
    }
}
