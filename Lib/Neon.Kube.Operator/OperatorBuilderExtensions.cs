//-----------------------------------------------------------------------------
// FILE:	    OperatorBuilderExtensions.cs
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
    /// Extension methods for adding components to the Operator.
    /// </para>
    /// </summary>
    public static class OperatorBuilderExtensions
    {
        /// <summary>
        /// Adds a <see cref="IResourceController{TEntity}"/> to the Operator.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <param name="namespace"></param>
        /// <param name="options"></param>
        /// <param name="leaderConfig"></param>
        /// <param name="leaderElectionDisabled"></param>
        /// <returns></returns>
        public static IOperatorBuilder AddController<TImplementation>(
            this IOperatorBuilder builder,
            string @namespace = null,
            ResourceManagerOptions options = null,
            LeaderElectionConfig leaderConfig = null,
            bool leaderElectionDisabled = false)
            where TImplementation : class
        {
            var entityTypes = typeof(TImplementation).GetInterfaces()
                .Where(
                    t =>
                        t.IsConstructedGenericType &&
                        t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceController<>)))
                .Select(i => i.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddController) && m.GetGenericArguments().Length == 2);

            foreach (var entityType in entityTypes)
            {
                var registrationMethod =
                    genericRegistrationMethod.MakeGenericMethod(typeof(TImplementation), entityType);
                var param = registrationMethod.GetParameters();
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
        /// Adds a <see cref="IResourceFinalizer{TEntity}"/> to the Operator.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IOperatorBuilder AddFinalizer<TImplementation>(this IOperatorBuilder builder)
            where TImplementation : class
        {
            var entityTypes = typeof(TImplementation).GetInterfaces()
                .Where(
                    t =>
                        t.IsConstructedGenericType &&
                        t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IResourceFinalizer<>)))
                .Select(i => i.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddFinalizer) && m.GetGenericArguments().Length == 2);

            foreach (var entityType in entityTypes)
            {
                var registrationMethod =
                    genericRegistrationMethod.MakeGenericMethod(typeof(TImplementation), entityType);
                var param = registrationMethod.GetParameters();
                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }

        /// <summary>
        /// Adds a <see cref="IMutatingWebhook{TEntity}"/> to the Operator.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IOperatorBuilder AddMutatingWebhook<TImplementation>(this IOperatorBuilder builder)
            where TImplementation : class
        {
            var entityTypes = typeof(TImplementation).GetInterfaces()
                .Where(
                    t =>
                        t.IsConstructedGenericType &&
                        t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IMutatingWebhook<>)))
                .Select(i => i.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddMutatingWebhook) && m.GetGenericArguments().Length == 2);

            foreach (var entityType in entityTypes)
            {
                var registrationMethod =
                    genericRegistrationMethod.MakeGenericMethod(typeof(TImplementation), entityType);
                var param = registrationMethod.GetParameters();
                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }

        /// <summary>
        /// Adds a <see cref="IValidatingWebhook{TEntity}"/> to the Operator.
        /// </summary>
        /// <typeparam name="TImplementation"></typeparam>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IOperatorBuilder AddValidatingWebhook<TImplementation>(this IOperatorBuilder builder)
            where TImplementation : class
        {
            var entityTypes = typeof(TImplementation).GetInterfaces()
                .Where(
                    t =>
                        t.IsConstructedGenericType &&
                        t.GetGenericTypeDefinition().IsEquivalentTo(typeof(IValidatingWebhook<>)))
                .Select(i => i.GenericTypeArguments[0]);

            var genericRegistrationMethod = builder
                .GetType()
                .GetMethods()
                .Single(m => m.Name == nameof(IOperatorBuilder.AddValidatingWebhook) && m.GetGenericArguments().Length == 2);

            foreach (var entityType in entityTypes)
            {
                var registrationMethod =
                    genericRegistrationMethod.MakeGenericMethod(typeof(TImplementation), entityType);
                var param = registrationMethod.GetParameters();
                registrationMethod.Invoke(builder, new object[registrationMethod.GetParameters().Count()]);
            }

            return builder;
        }
    }
}
