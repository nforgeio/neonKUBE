//-----------------------------------------------------------------------------
// FILE:	    ComponentRegister.cs
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
    internal class ComponentRegister
    {
        public HashSet<ControllerRegistration> ControllerRegistrations { get; set; }
        public HashSet<EntityRegistration> EntityRegistrations { get; set; }
        public HashSet<FinalizerRegistration> FinalizerRegistrations { get; set; }
        public HashSet<MutatingWebhookRegistration> MutatingWebhookRegistrations { get; set; }
        public HashSet<Type> ResourceManagerRegistrations { get; set; }
        public HashSet<ValidatingWebhookRegistration> ValidatingWebhookRegistrations { get; set; }

        public ComponentRegister()
        {
            ControllerRegistrations = new HashSet<ControllerRegistration>();
            EntityRegistrations = new HashSet<EntityRegistration>();
            FinalizerRegistrations = new HashSet<FinalizerRegistration>();
            MutatingWebhookRegistrations = new HashSet<MutatingWebhookRegistration>();
            ResourceManagerRegistrations = new HashSet<Type>();
            ValidatingWebhookRegistrations = new HashSet<ValidatingWebhookRegistration>();
        }
        public void RegisterController<TController, TEntity>()
            where TController : class, IResourceController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            ControllerRegistrations.Add(new ControllerRegistration(typeof(TController), typeof(TEntity)));

            return;
        }
        public void RegisterEntity<TEntity>()
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            EntityRegistrations.Add(new EntityRegistration(typeof(TEntity)));

            return;
        }
        public void RegisterFinalizer<TFinalizer, TEntity>()
            where TFinalizer : class, IResourceFinalizer<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            FinalizerRegistrations.Add(new FinalizerRegistration(typeof(TFinalizer), typeof(TEntity)));

            return;
        }
        public void RegisterMutatingWebhook<TMutator, TEntity>()
            where TMutator : class, IMutatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            MutatingWebhookRegistrations.Add(new MutatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }
        public void RegisterResourceManager<TResourceManager>()
        {
            ResourceManagerRegistrations.Add(typeof(TResourceManager));

            return;
        }
        public void RegisterValidatingWebhook<TMutator, TEntity>()
            where TMutator : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            ValidatingWebhookRegistrations.Add(new ValidatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }
    }

    internal record ControllerRegistration(Type ControllerType, Type EntityType);
    internal record EntityRegistration(Type EntityType);
    internal record FinalizerRegistration(Type FinalizerType, Type EntityType);
    internal record MutatingWebhookRegistration(Type WebhookType, Type EntityType);
    internal record ResourceManagerRegistration(Type ResourceManagerType);
    internal record ValidatingWebhookRegistration(Type WebhookType, Type EntityType);
}
