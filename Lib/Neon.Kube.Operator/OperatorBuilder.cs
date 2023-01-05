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
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;

using k8s.Models;
using k8s;
using Neon.Kube.Resources.Cluster;
using System.Resources;

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
            KubeHelper.InitializeJson();

            if (!Services.Any(x => x.ServiceType == typeof(IKubernetes)))
            {
                var k8s = new Kubernetes(
                    KubernetesClientConfiguration.BuildDefaultConfig(), 
                    new KubernetesRetryHandler());
                Services.AddSingleton(k8s);
            }

            Services.AddSingleton(componentRegister);
            Services.AddSingleton<IFinalizerBuilder, FinalizerBuilder>();
            Services.AddTransient(typeof(IFinalizerManager<>), typeof(FinalizerManager<>));
            Services.AddScoped(typeof(IResourceCache<>), typeof(ResourceCache<>));
            Services.AddScoped(typeof(ILockProvider<>), typeof(LockProvider<>));
            Services.AddHostedService<ResourceControllerManager>();

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
        public IOperatorBuilder AddController<TImplementation, TEntity>(
            string                  @namespace             = null,
            ResourceManagerOptions  options                = null,
            Func<TEntity, bool>     filter                 = null,
            LeaderElectionConfig    leaderConfig           = null,
            bool                    leaderElectionDisabled = false)
            where TImplementation : class, IOperatorController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            var resourceManager = new ResourceManager<TEntity, TImplementation>(
                serviceProvider: Services.BuildServiceProvider(),
                @namespace: @namespace,
                options: options,
                filter: filter,
                leaderConfig: leaderConfig,
                leaderElectionDisabled: leaderElectionDisabled);
            
            Services.AddSingleton(resourceManager);
            componentRegister.RegisterResourceManager<ResourceManager<TEntity, TImplementation>>();

            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterController<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddNgrokTunnnel(
            string hostname = "localhost", 
            int port = 5000,
            string ngrokDirectory = null,
            string ngrokAuthToken = null,
            bool enabled = true)
        {
            if (!enabled)
            {
                return this;
            }

            Services.AddHostedService<NgrokWebhookTunnel>(
                services => new NgrokWebhookTunnel(
                    services.GetRequiredService<IKubernetes>(),
                    componentRegister,
                    services,
                    ngrokDirectory,
                    ngrokAuthToken,
                    services.GetService<ILogger>())
                { 
                    Host                       = hostname,
                    Port                       = port
                });

            return this;
        }
    }
}
