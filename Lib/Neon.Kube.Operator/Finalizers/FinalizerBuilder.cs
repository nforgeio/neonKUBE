//-----------------------------------------------------------------------------
// FILE:	    FinalizerBuilder.cs
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
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Kube.Operator.Builder;

using k8s.Models;
using k8s;
using Neon.BuildInfo;
using Neon.Diagnostics;

namespace Neon.Kube.Operator.Finalizer
{
    internal class FinalizerBuilder : IFinalizerBuilder
    {
        private readonly ComponentRegister componentRegister;

        public FinalizerBuilder(ComponentRegister componentRegister)
        {
            this.componentRegister = componentRegister;
        }

        /// <inheritdoc/>
        public IResourceFinalizer<TEntity> BuildFinalizer<TEntity, TFinalizer>(IServiceProvider serviceProvider)
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                return componentRegister.FinalizerRegistrations
                .Where(r => r.EntityType.IsEquivalentTo(typeof(TEntity)))
                .Where(r => r.FinalizerType.IsEquivalentTo(typeof(TFinalizer)))
                .Select(r => (IResourceFinalizer<TEntity>)serviceProvider.GetRequiredService(r.FinalizerType))
                .Single();
            }
        }

        /// <inheritdoc/>
        public IEnumerable<IResourceFinalizer<TEntity>> BuildFinalizers<TEntity>(IServiceProvider serviceProvider)
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                return componentRegister.FinalizerRegistrations
                    .Where(r => r.EntityType.IsEquivalentTo(typeof(TEntity)))
                    .Select(r => (IResourceFinalizer<TEntity>)serviceProvider.GetRequiredService(r.FinalizerType));
            }
        }
    }

}
