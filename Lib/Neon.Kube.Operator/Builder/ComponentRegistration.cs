//-----------------------------------------------------------------------------
// FILE:	    ComponentRegistration.cs
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

using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Webhook;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator.Builder
{
    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    internal class ComponentRegistration
    {
        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public ComponentRegistration()
        {
            ControllerRegistrations        = new HashSet<ControllerRegistration>();
            EntityRegistrations            = new HashSet<EntityRegistration>();
            FinalizerRegistrations         = new HashSet<FinalizerRegistration>();
            MutatingWebhookRegistrations   = new HashSet<MutatingWebhookRegistration>();
            ResourceManagerRegistrations   = new HashSet<Type>();
            ValidatingWebhookRegistrations = new HashSet<ValidatingWebhookRegistration>();
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<ControllerRegistration> ControllerRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<EntityRegistration> EntityRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<FinalizerRegistration> FinalizerRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<MutatingWebhookRegistration> MutatingWebhookRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<Type> ResourceManagerRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        public HashSet<ValidatingWebhookRegistration> ValidatingWebhookRegistrations { get; set; }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TController"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        public void RegisterController<TController, TEntity>()
            where TController : class, IResourceController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            ControllerRegistrations.Add(new ControllerRegistration(typeof(TController), typeof(TEntity)));

            return;
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        public void RegisterEntity<TEntity>()
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            EntityRegistrations.Add(new EntityRegistration(typeof(TEntity)));

            return;
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TFinalizer"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        public void RegisterFinalizer<TFinalizer, TEntity>()
            where TFinalizer : class, IResourceFinalizer<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            FinalizerRegistrations.Add(new FinalizerRegistration(typeof(TFinalizer), typeof(TEntity)));

            return;
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TMutator"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        public void RegisterMutatingWebhook<TMutator, TEntity>()
            where TMutator : class, IMutatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            MutatingWebhookRegistrations.Add(new MutatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TResourceManager"></typeparam>
        public void RegisterResourceManager<TResourceManager>()
        {
            ResourceManagerRegistrations.Add(typeof(TResourceManager));

            return;
        }

        /// <summary>
        /// $todo(marcusbooyah): documentation
        /// </summary>
        /// <typeparam name="TMutator"></typeparam>
        /// <typeparam name="TEntity"></typeparam>
        public void RegisterValidatingWebhook<TMutator, TEntity>()
            where TMutator : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            ValidatingWebhookRegistrations.Add(new ValidatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }
    }

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="ControllerType"></param>
    /// <param name="EntityType"></param>
    internal record ControllerRegistration(Type ControllerType, Type EntityType);

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="EntityType"></param>
    internal record EntityRegistration(Type EntityType);

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="FinalizerType"></param>
    /// <param name="EntityType"></param>
    internal record FinalizerRegistration(Type FinalizerType, Type EntityType);

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="WebhookType"></param>
    /// <param name="EntityType"></param>
    internal record MutatingWebhookRegistration(Type WebhookType, Type EntityType);

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="ResourceManagerType"></param>
    internal record ResourceManagerRegistration(Type ResourceManagerType);

    /// <summary>
    /// $todo(marcusbooyah): documentation
    /// </summary>
    /// <param name="WebhookType"></param>
    /// <param name="EntityType"></param>
    internal record ValidatingWebhookRegistration(Type WebhookType, Type EntityType);
}
