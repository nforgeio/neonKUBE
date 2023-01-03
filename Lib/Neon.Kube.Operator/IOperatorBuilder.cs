//-----------------------------------------------------------------------------
// FILE:	    IOperatorBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Operator  builder interface.
    /// </summary>
    public interface IOperatorBuilder
    {
        /// <summary>
        /// Returns the original service collection.
        /// </summary>
        IServiceCollection Services { get; }

        /// <summary>
        /// <para>
        /// Adds a CRD controller to the operator.
        /// </para>
        /// </summary>
        /// <typeparam name="TImplementation">The type of the controller to register.</typeparam>
        /// <typeparam name="TEntity">The type of the entity to associate the controller with.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IOperatorBuilder AddController<TImplementation, TEntity>()
            where TImplementation : class, IOperatorController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new();

        /// <summary>
        /// <para>
        /// Adds a CRD finalizer to the operator.
        /// </para>
        /// </summary>
        /// <typeparam name="TImplementation">The type of the finalizer to register.</typeparam>
        /// <typeparam name="TEntity">The type of the entity to associate the finalizer with.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IOperatorBuilder AddFinalizer<TImplementation, TEntity>()
            where TImplementation : class, IResourceFinalizer<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new();

        /// <summary>
        /// <para>
        /// Adds a mutating webhook to the operator.
        /// </para>
        /// </summary>
        /// <typeparam name="TImplementation">The type of the webhook to register.</typeparam>
        /// <typeparam name="TEntity">The type of the entity to associate the webhook with.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IOperatorBuilder AddMutatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IMutatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new();

        /// <summary>
        /// <para>
        /// Adds a validating webhook to the operator.
        /// </para>
        /// </summary>
        /// <typeparam name="TImplementation">The type of the webhook to register.</typeparam>
        /// <typeparam name="TEntity">The type of the entity to associate the webhook with.</typeparam>
        /// <returns>The builder for chaining.</returns>
        IOperatorBuilder AddValidatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new();

        /// <summary>
        /// <para>
        /// For development purposes only. Adds a tunnel and configures webhooks to 
        /// tunnel through to the developer workstation.
        /// </para>
        /// </summary>
        /// <param name="hostname">The hostname for the tunnel.</param>
        /// <param name="port">The port.</param>
        /// <param name="ngrokDirectory">The directory where the ngrok binary is located.</param>
        /// <param name="ngrokAuthToken">The ngrok auth token</param>
        /// <param name="enabled">Set to false to optionally disable this feature.</param>
        /// <returns></returns>
        IOperatorBuilder AddNgrokTunnnel(
            string hostname = "localhost",
            int port = 5000,
            string ngrokDirectory = null,
            string ngrokAuthToken = null,
            bool enabled = true);
    }
}
