//-----------------------------------------------------------------------------
// FILE:	    DependentResource.cs
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube.Operator.Attributes;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Prometheus;
using Neon.Kube.Resources;

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// <para>
    /// Defines a dependent resource. This allows the Operator to respond to updates to Dependent resources.
    /// For example, a Deployment will create a ReplicaSet and the Deployment controller may want to Reconcile
    /// when there are updates to the ReplicaSet.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public class DependentResource<TEntity> : IDependentResource
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        /// <inheritdoc/>
        public string WatchNamespace { get; set; } = null;
        
        /// <inheritdoc/>
        public EntityScope Scope { get; set; } = EntityScope.Namespaced;

        /// <summary>
        /// Constructor
        /// </summary>
        public DependentResource(
            string watchNamespace = null,
            EntityScope scope = EntityScope.Namespaced)
        {
            this.WatchNamespace = watchNamespace;
            this.Scope = scope;
        }

        /// <inheritdoc/>
        public string Namespace()
        {
            return WatchNamespace;
        }

        /// <inheritdoc/>
        public Type GetEntityType()
        {
            return typeof(TEntity);
        }

        /// <inheritdoc/>
        public KubernetesEntityAttribute GetKubernetesEntityAttribute()
        {
            return GetEntityType().GetKubernetesTypeMetadata();
        }
    }
}
