//-----------------------------------------------------------------------------
// FILE:	    ContainerRegistryController.cs
// CONTRIBUTOR: Marcus Bowyer, Jeff Lill
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Entities;

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
        /// Called when an entity is created on the API Server.
        /// </summary>
        /// <param name="entity">The new entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> CreatedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(CreatedAsync)}.");

            return await Task.FromResult(ResourceControllerResult.RequeueEvent(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// Called when an entity is updated on the API server.
        /// </summary>
        /// <param name="entity">The updated entity.</param>
        /// <returns>The controller result.</returns>
        public async Task<ResourceControllerResult> UpdatedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(UpdatedAsync)}.");

            return await Task.FromResult(ResourceControllerResult.RequeueEvent(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// Called when an entity has not been modified when a requeued event is raised.
        /// </summary>
        /// <param name="entity">The unmodified entity.</param>
        /// <returns>The optional <see cref="ResourceControllerResult"/> indicating whether the event should be requeued.</returns>
        public async Task<ResourceControllerResult> NotModifiedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(NotModifiedAsync)}.");

            return await Task.FromResult(ResourceControllerResult.RequeueEvent(TimeSpan.FromSeconds(5)));
        }

        /// <summary>
        /// Called when the status of an entity has been modified on the API server.
        /// </summary>
        /// <param name="entity">The modified entity.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task StatusModifiedAsync(V1ContainerRegistry entity)
        {
            log.LogInfo($"entity {entity.Name()} called {nameof(StatusModifiedAsync)}.");

            await Task.CompletedTask;
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
