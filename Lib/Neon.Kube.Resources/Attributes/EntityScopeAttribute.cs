//-----------------------------------------------------------------------------
// FILE:        EntityScopeAttribute.cs
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
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Indicates whether the defined custom resource is cluster- or namespace-scoped. 
    /// Allowed values are `Cluster` and `Namespaced`.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class EntityScopeAttribute : Attribute
    {
        /// <summary>
        /// The <see cref="EntityScope"/>.
        /// </summary>
        public EntityScope Scope { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="scope">Specifies the entity scope, one of the <see cref="EntityScope"/> values.</param>
        public EntityScopeAttribute(EntityScope scope = EntityScope.Namespaced)
        {
            Scope = scope;
        }
    }
}
