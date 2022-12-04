//-----------------------------------------------------------------------------
// FILE:	    OperatorBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Microsoft.Extensions.DependencyInjection.Extensions;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// <para>
    /// Used to build a kubernetes operator.
    /// </para>
    /// </summary>
    public class OperatorBuilder : IOperatorBuilder
    {
        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        private ComponentRegister componentRegister { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="services"></param>
        public OperatorBuilder(IServiceCollection services)
        {
            Services          = services;
            componentRegister = new ComponentRegister();
        }

        internal IOperatorBuilder AddOperatorBase()
        {
            if (!Services.Any(x => x.ServiceType == typeof(IKubernetes)))
            {
                var k8s = new Kubernetes(
                    KubernetesClientConfiguration.BuildDefaultConfig(), 
                    new RetryHandler());
                Services.AddSingleton(k8s);
            }

            Services.AddSingleton(componentRegister);
            Services.AddSingleton<IFinalizerBuilder, FinalizerBuilder>();
            Services.AddTransient(typeof(IFinalizerManager<>), typeof(FinalizerManager<>));
            Services.AddScoped(typeof(IResourceCache<>), typeof(ResourceCache<>));
            Services.AddScoped(typeof(ILockProvider<>), typeof(LockProvider<>));

            Services.AddRouting();
            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddFinalizer<TImplementation, TEntity>()
            where TImplementation : class, IResourceFinalizer<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterFinalizer<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddMutatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IMutatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterMutatingWebhook<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddValidatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterValidatingWebhook<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddController<TImplementation, TEntity>()
            where TImplementation : class, IOperatorController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterController<TImplementation, TEntity>();

            return this;
        }
    }
}
