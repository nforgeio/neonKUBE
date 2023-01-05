//-----------------------------------------------------------------------------
// FILE:	    IOperatorController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Tasks;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Prometheus;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Describes the interface used to implement Neon based operator controllers.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the Kubernetes entity being managed.</typeparam>
    public interface IOperatorController<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static Task StartAsync(IServiceProvider serviceProvider)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global operations.
        /// The period is controlled by <see cref="ResourceManagerOptions.IdleInterval"/>.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task IdleAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a new resource is detected or when the non-status part of an existing resource
        /// is modified.
        /// </summary>
        /// <param name="entity">The new or modified resource.</param>
        /// <returns>
        /// A <see cref="ResourceControllerResult"/> indicating the the current event or possibly a new event is 
        /// to be requeue with a possible delay.  <c>null</c> may also bne returned, indicating that
        /// the event is not to be requeued.
        /// </returns>
        public Task<ResourceControllerResult> ReconcileAsync(TEntity entity)
        {
            return Task.FromResult<ResourceControllerResult>(null);
        }

        /// <summary>
        /// Called when the status part of a resource has been modified.
        /// </summary>
        /// <param name="entity">The modified resource.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task StatusModifiedAsync(TEntity entity)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when a resource has been deleted.
        /// </summary>
        /// <param name="entity">The deleted resource.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task DeletedAsync(TEntity entity)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and this instance has
        /// assumed leadership.
        /// </summary>
        public async Task OnPromotionAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> this instance has
        /// been demoted.
        /// </summary>
        public async Task OnDemotionAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and a new leader has
        /// been elected.
        /// </summary>
        /// <param name="identity">Identifies the new leader.</param>
        public async Task OnNewLeaderAsync(string identity)
        {
            await Task.CompletedTask;
        }
    }
}
