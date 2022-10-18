//-----------------------------------------------------------------------------
// FILE:	    ComponentRegister.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    internal class ComponentRegister
    {
        public HashSet<ControllerRegistration> ControllerRegistrations { get; set; }
        public HashSet<MutatingWebhookRegistration> MutatingWebhookRegistrations { get; set; }

        public ComponentRegister() 
        {
            ControllerRegistrations      = new HashSet<ControllerRegistration>();
            MutatingWebhookRegistrations = new HashSet<MutatingWebhookRegistration>();
        }
        public void RegisterController<TController, TEntity>()
            where TController : class, IOperatorController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            ControllerRegistrations.Add(new ControllerRegistration(typeof(TController), typeof(TEntity)));

            return;
        }
        public void RegisterMutatingWebhook<TMutator, TEntity>()
            where TMutator : class, IMutationWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            MutatingWebhookRegistrations.Add(new MutatingWebhookRegistration(typeof(TMutator), typeof(TEntity)));

            return;
        }
    }

    internal record ControllerRegistration(Type controllerType, Type EntityType);
    internal record MutatingWebhookRegistration(Type WebhookType, Type EntityType);
}
