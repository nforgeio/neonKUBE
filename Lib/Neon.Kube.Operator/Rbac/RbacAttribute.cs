//-----------------------------------------------------------------------------
// FILE:	    RbacAttribute.cs
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

using Neon.Kube.Resources;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator.Rbac
{
    /// <summary>
    /// Used to exclude a component from assembly scanning when building the operator.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class RbacAttribute<TEntity> : Attribute, IRbacAttribute
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        /// <summary>
        /// The list of verbs describing the allowed actions.
        /// </summary>
        public RbacVerb Verbs { get; set; } = RbacVerb.None;

        /// <summary>
        /// The <see cref="EntityScope"/> of the permission.
        /// </summary>
        public EntityScope Scope { get; set; } = EntityScope.Namespaced;

        /// <summary>
        /// The namespace for the attribute if <see cref="Scope"/> is <see cref="EntityScope.Namespaced"/>.
        /// </summary>
        public string Namespace { get; set; } = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public RbacAttribute(
            RbacVerb verbs = RbacVerb.None,
            EntityScope scope = EntityScope.Namespaced,
            string @namespace = null)
        {
            this.Verbs     = verbs;
            this.Scope     = scope;
            this.Namespace = @namespace;
        }

        public Type GetEntityType()
        {
            return typeof(TEntity);
        }
    }
}
