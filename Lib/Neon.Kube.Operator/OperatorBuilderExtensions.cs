//-----------------------------------------------------------------------------
// FILE:        OperatorBuilderExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

using Neon.Diagnostics;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Cache;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Webhook;
using Neon.Kube.Operator.Webhook.Ngrok;

using k8s.Models;
using k8s;
using Microsoft.AspNetCore.Components;
using Neon.BuildInfo;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// <para>
    /// Extension methods for adding components to the operator.
    /// </para>
    /// </summary>
    public static class OperatorBuilderExtensions
    {
        /// <summary>
        /// Adds a <see cref="IResourceController{TEntity}"/> implementation to the operator.
        /// </summary>
        /// <typeparam name="TResourceController">Specifies the controller type.</typeparam>
        /// <param name="builder">Specifies the operator builder.</param>
        /// <param name="namespace">Optionally specifies the operator namespace.</param>
        /// <param name="options">Optionally specifies the resource manager options.</param>
        /// <param name="leaderConfig">Optionally specifies a custom leader electionb configuration.</param>
        /// <param name="leaderElectionDisabled">Optionally indicates that leader election is disabled.</param>
        /// <returns>The <see cref="IOperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddController<TResourceController>(
            this IOperatorBuilder   builder,
            string                  @namespace             = null,
            ResourceManagerOptions  options                = null,
            LeaderElectionConfig    leaderConfig           = null,
            bool                    leaderElectionDisabled = false)

            where TResourceController : class
        {
            var interfaces = typeof(TResourceController).GetInterfaces()
                .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceController<>)))
                .Select(@interface => @interface.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(method => method.Name == nameof(IOperatorBuilder.AddController) && method.GetGenericArguments().Length == 2);

            foreach (var @interface in interfaces)
            {
                var registrationMethod = genericRegistrationMethod.MakeGenericMethod(typeof(TResourceController), @interface);
                var param              = registrationMethod.GetParameters();

                registrationMethod.Invoke(builder, new object[]
                {
                    @namespace,
                    options,
                    leaderConfig,
                    leaderElectionDisabled
                });
            }

            return builder;
        }

        /// <summary>
        /// Adds a <see cref="IResourceFinalizer{TEntity}"/> to the operator.
        /// </summary>
        /// <typeparam name="TResourceController">Specifies the controller type.</typeparam>
        /// <param name="builder">Specifies the operator builder.</param>
        /// <returns>The <see cref="IOperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddFinalizer<TResourceController>(this IOperatorBuilder builder)
            where TResourceController : class
        {
            var interfaces = typeof(TResourceController).GetInterfaces()
                .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceFinalizer<>)))
                .Select(@interface => @interface.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddFinalizer) && m.GetGenericArguments().Length == 2);

            foreach (var @interface in interfaces)
            {
                var registrationMethod = genericRegistrationMethod.MakeGenericMethod(typeof(TResourceController), @interface);
                var param              = registrationMethod.GetParameters();

                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }

        /// <summary>
        /// Adds a <see cref="IMutatingWebhook{TEntity}"/> to the operator.
        /// </summary>
        /// <typeparam name="TResourceController">Specifies the controller type.</typeparam>
        /// <param name="builder">Specifies the operator builder.</param>
        /// <returns>The <see cref="IOperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddMutatingWebhook<TResourceController>(this IOperatorBuilder builder)
            where TResourceController : class
        {
            var interfaces = typeof(TResourceController).GetInterfaces()
                .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IMutatingWebhook<>)))
                .Select(@interface => @interface.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddMutatingWebhook) && m.GetGenericArguments().Length == 2);

            foreach (var @interface in interfaces)
            {
                var registrationMethod = genericRegistrationMethod.MakeGenericMethod(typeof(TResourceController), @interface);
                var param              = registrationMethod.GetParameters();

                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }

        /// <summary>
        /// Adds a <see cref="IValidatingWebhook{TEntity}"/> to the operator.
        /// </summary>
        /// <typeparam name="TResourceController">Specifies the controller type.</typeparam>
        /// <param name="builder">Specifies the operator builder.</param>
        /// <returns>The <see cref="IOperatorBuilder"/>.</returns>
        public static IOperatorBuilder AddValidatingWebhook<TResourceController>(this IOperatorBuilder builder)
            where TResourceController : class
        {
            var interfaces = typeof(TResourceController).GetInterfaces()
                .Where(@interface => @interface.IsConstructedGenericType && @interface.GetGenericTypeDefinition().IsEquivalentTo(typeof(IValidatingWebhook<>)))
                .Select(@interface => @interface.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddValidatingWebhook) && m.GetGenericArguments().Length == 2);

            foreach (var @interface in interfaces)
            {
                var registrationMethod = genericRegistrationMethod.MakeGenericMethod(typeof(TResourceController), @interface);
                var param              = registrationMethod.GetParameters();

                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }
    }
}
