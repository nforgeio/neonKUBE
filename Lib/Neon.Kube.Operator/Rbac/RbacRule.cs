//-----------------------------------------------------------------------------
// FILE:	    RbacRule.cs
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
using System.Reflection;

using Neon.Kube.Resources;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator.Rbac
{
    /// <summary>
    /// Models an RBAC rule.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the target entity type.</typeparam>
    public class RbacRule<TEntity> : IRbacRule
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="verbs">Specifies the RBAC verbs.</param>
        /// <param name="scope">Specifies whether the entity is namespaced or cluster scoped.</param>
        /// <param name="namespaces">Optionally specifies a common separated list of namespaces to access.</param>
        /// <param name="resourceNames">Optionally specifies a comma separated list of the names of specific resources to be accessed.</param>/param>
        /// <param name="subResources">Optionally specifies a comma separated list of subresource names.</param>
        public RbacRule(
            RbacVerb    verbs         = RbacVerb.None,
            EntityScope scope         = EntityScope.Namespaced,
            string      namespaces    = null,
            string      resourceNames = null,
            string      subResources  = null)
        {
            this.Verbs         = verbs;
            this.Scope         = scope;
            this.Namespaces    = namespaces;
            this.ResourceNames = resourceNames;
            this.SubResources  = subResources;

            if (typeof(TEntity).GetCustomAttribute<EntityScopeAttribute>()?.Scope == EntityScope.Cluster)
            {
                this.Scope = EntityScope.Cluster;
            }
        }

        /// <summary>
        /// The <see cref="RbacVerb"/> with the verb bits.
        /// </summary>
        public RbacVerb Verbs { get; set; } = RbacVerb.None;

        /// <summary>
        /// The <see cref="EntityScope"/> of the permission.
        /// </summary>
        public EntityScope Scope { get; set; } = EntityScope.Namespaced;

        /// <summary>
        /// Comma separated list of namespaces to watch or <c>null</c>.
        /// </summary>
        public string Namespaces { get; set; } = null;

        /// <summary>
        /// Comma separated list of resource names to restrict access to individual 
        /// instances of a resource or <c>null</c>.
        /// </summary>
        public string ResourceNames { get; set; } = null;

        /// <summary>
        /// Comma separated list of subresources or <c>null</c>.
        /// </summary>
        public string SubResources { get; set; } = null;

        /// <summary>
        /// Returns <see cref="Namespaces"/> as a list of strings or <c>null</c>
        /// when <see cref="Namespaces"/> is <c>null</c>.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> NamespaceList()
        {
            return Namespaces?.Split(',') ?? null;
        }

        /// <summary>
        /// Returns the entity type.
        /// </summary>
        /// <returns></returns>
        public Type GetEntityType() => typeof(TEntity);

        /// <summary>
        /// Returns the associated <see cref="KubernetesEntityAttribute"/>.
        /// </summary>
        /// <returns>The <see cref="KubernetesEntityAttribute"/>.</returns>
        public KubernetesEntityAttribute GetKubernetesEntityAttribute() => GetEntityType().GetKubernetesTypeMetadata();
    }
}
