//-----------------------------------------------------------------------------
// FILE:	    OperatorBuilder.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Cache;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Webhook;
using Neon.Kube.Operator.Webhook.Ngrok;

using AsyncKeyedLock;

using k8s.Models;
using k8s;
using k8s.KubeConfigModels;

using Prometheus;

namespace Neon.Kube.Operator.Builder
{
    /// <summary>
    /// <para>
    /// Used to build a kubernetes operator.
    /// </para>
    /// </summary>
    public class OperatorBuilder : IOperatorBuilder
    {
        internal static string StartupProbeTag = "startup";
        internal static string LivenessProbeTag = "liveness";
        internal static string ReadinessProbeTag = "readiness";

        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        private ComponentRegister componentRegister { get; }

        private OperatorSettings operatorSettings { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="services"></param>
        public OperatorBuilder(IServiceCollection services)
        {
            Services = services;
            componentRegister = new ComponentRegister();
        }

        internal IOperatorBuilder AddOperatorBase()
        {
            operatorSettings = (OperatorSettings)Services.Where(s => s.ServiceType == typeof(OperatorSettings)).Single().ImplementationInstance;

            if (!Services.Any(x => x.ServiceType == typeof(IKubernetes)))
            {
                var k8sClientConfig = operatorSettings.KubernetesClientConfiguration ?? KubernetesClientConfiguration.BuildDefaultConfig();

                var k8s = new Kubernetes(
                    k8sClientConfig,
                    new KubernetesRetryHandler());

                if (NeonHelper.IsDevWorkstation ||
                    Debugger.IsAttached)
                {
                    k8s.HttpClient.DefaultRequestHeaders.Add("Impersonate-User", $"system:serviceaccount:{operatorSettings.deployedNamespace}:{operatorSettings.Name}"); 
                }

                Services.AddSingleton<IKubernetes>(k8s);
            }

            Services.AddSingleton<OperatorSettings>(operatorSettings);
            Services.AddSingleton(operatorSettings.ResourceManagerOptions);
            Services.AddSingleton(componentRegister);
            Services.AddSingleton<IFinalizerBuilder, FinalizerBuilder>();
            Services.AddSingleton(typeof(IFinalizerManager<>), typeof(FinalizerManager<>));
            Services.AddSingleton(typeof(ICrdCache), typeof(CrdCache));
            Services.AddSingleton(typeof(IResourceCache<>), typeof(ResourceCache<>));
            Services.AddSingleton(new AsyncKeyedLocker<string>(o =>
            {
                o.PoolSize = operatorSettings.LockPoolSize;
                o.PoolInitialFill = operatorSettings.LockPoolInitialFill;
            }));

            if (operatorSettings.AssemblyScanningEnabled)
            {
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(
                        t => t.GetInterfaces().Any(i => i.GetCustomAttributes<OperatorComponentAttribute>().Any())
                    );

                foreach (var type in types)
                {
                    switch (type.GetInterfaces().Where(i => i.GetCustomAttributes<OperatorComponentAttribute>().Any()).Select(i => i.GetCustomAttribute<OperatorComponentAttribute>()).FirstOrDefault().ComponentType)
                    {
                        case OperatorComponentType.Controller:

                            var controllerRegMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddController));
                            var controllerArgs      = new object[controllerRegMethod.GetParameters().Count()];
                            controllerArgs[0]       = this;

                            var options = new ResourceManagerOptions();
                            var controllerAttribute = type.GetCustomAttribute<ControllerAttribute>();

                            if (controllerAttribute?.Ignore == true)
                            {
                                break;
                            }

                            if (operatorSettings.WatchNamespace != null)
                            {
                                options.WatchNamespace = operatorSettings.WatchNamespace;
                            }

                            if (controllerAttribute?.AutoRegisterFinalizers == false)
                            {
                                options.AutoRegisterFinalizers = false;
                            }

                            if (controllerAttribute?.ManageCustomResourceDefinitions == false)
                            {
                                options.ManageCustomResourceDefinitions = false;
                            }

                            var dependentResources = type.GetCustomAttributes()
                                                            .Where(a => a.GetType().IsGenericType)
                                                            .Where(a => a.GetType()
                                                                    .GetGenericTypeDefinition()
                                                                    .IsEquivalentTo(typeof(DependentResourceAttribute<>)))
                                                            .Select(a => (IDependentResource)(a)).ToList();

                            foreach (var dr in dependentResources)
                            {
                                if (!options.DependentResources.Any(t => t.GetEntityType() == dr.GetEntityType()))
                                {
                                    options.DependentResources.Add(dr);
                                }
                            }

                            controllerArgs[2] = options;
                            controllerRegMethod.MakeGenericMethod(type).Invoke(null, controllerArgs);

                            break;

                        case OperatorComponentType.Finalizer:

                            if (type.GetCustomAttribute<FinalizerAttribute>()?.Ignore == true)
                            {
                                break;
                            }

                            var finalizerRegMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddFinalizer));
                            var finalizerRegArgs = new object[finalizerRegMethod.GetParameters().Count()];
                            finalizerRegArgs[0] = this;
                            finalizerRegMethod.MakeGenericMethod(type).Invoke(null, finalizerRegArgs);

                            break;

                        case OperatorComponentType.MutationWebhook:

                            if (type.GetCustomAttribute<MutatingWebhookAttribute>()?.Ignore == true)
                            {
                                break;
                            }

                            var mutatingWebhookRegMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddMutatingWebhook));
                            var mutatingWebhookRegArgs = new object[mutatingWebhookRegMethod.GetParameters().Count()];
                            mutatingWebhookRegArgs[0] = this;
                            mutatingWebhookRegMethod.MakeGenericMethod(type).Invoke(null, mutatingWebhookRegArgs);

                            break;

                        case OperatorComponentType.ValidationWebhook:

                            if (type.GetCustomAttribute<ValidatingWebhookAttribute>()?.Ignore == true)
                            {
                                break;
                            }

                            var validatingWebhookRegMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddValidatingWebhook));
                            var validatingWebhookRegArgs = new object[validatingWebhookRegMethod.GetParameters().Count()];
                            validatingWebhookRegArgs[0] = this;
                            validatingWebhookRegMethod.MakeGenericMethod(type).Invoke(null, validatingWebhookRegArgs);

                            break;
                    }
                }
            }

            Services.AddHealthChecks().ForwardToPrometheus();
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

            operatorSettings.hasMutatingWebhooks = true;

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddValidatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterValidatingWebhook<TImplementation, TEntity>();

            operatorSettings.hasValidatingWebhooks = true;

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddController<TImplementation, TEntity>(
            string @namespace = null,
            ResourceManagerOptions options = null,
            LeaderElectionConfig leaderConfig = null,
            bool leaderElectionDisabled = false)
            where TImplementation : class, IResourceController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddSingleton<ResourceManager<TEntity, TImplementation>>(services =>
            {
                return new ResourceManager<TEntity, TImplementation>(
                    serviceProvider: services,
                    options: options ?? operatorSettings.ResourceManagerOptions,
                    leaderConfig: leaderConfig,
                    leaderElectionDisabled: leaderElectionDisabled);
            });
            componentRegister.RegisterResourceManager<ResourceManager<TEntity, TImplementation>>();

            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterController<TImplementation, TEntity>();

            if (!leaderElectionDisabled)
            {
                operatorSettings.leaderElectionEnabled = true;
            }

            if (options?.ManageCustomResourceDefinitions == false)
            {
                operatorSettings.manageCustomResourceDefinitions = false;
            }

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

            Services.AddHostedService(
                services => new NgrokWebhookTunnel(
                    services.GetRequiredService<IKubernetes>(),
                    componentRegister,
                    services,
                    ngrokDirectory,
                    ngrokAuthToken)
                {
                    Host = hostname,
                    Port = port
                });

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddStartupCheck<TStartupCheck>(string name = null)
            where TStartupCheck : class, IHealthCheck
        {
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(TStartupCheck).Name;
            }

            Services.AddHealthChecks().AddCheck<TStartupCheck>(name, HealthStatus.Unhealthy, new string[] { StartupProbeTag });

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddLivenessCheck<TLivenessCheck>(string name = null)
            where TLivenessCheck : class, IHealthCheck
        {
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(TLivenessCheck).Name;
            }

            Services.AddHealthChecks().AddCheck<TLivenessCheck>(name, HealthStatus.Unhealthy, new string[] { LivenessProbeTag });

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddReadinessCheck<TReadinessCheck>(string name = null)
            where TReadinessCheck : class, IHealthCheck
        {
            if (string.IsNullOrEmpty(name))
            {
                name = typeof(TReadinessCheck).Name;
            }

            Services.AddHealthChecks().AddCheck<TReadinessCheck>(name, HealthStatus.Unhealthy, new string[] { ReadinessProbeTag });

            return this;
        }
    }
}
