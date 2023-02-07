//-----------------------------------------------------------------------------
// FILE:	    EntityExtensions.cs
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

using k8s.Models;
using k8s;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Entity extension methods.
    /// </summary>
    public static class EntityExtensions
    {
        /// <summary>
        /// Makes an owner reference from a <see cref="IKubernetesObject{V1ObjectMeta}"/>
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="blockOwnerDeletion"></param>
        /// <param name="controller"></param>
        /// <returns></returns>
        public static V1OwnerReference MakeOwnerReference(
            this IKubernetesObject<V1ObjectMeta> entity,
            bool? blockOwnerDeletion = null,
            bool? controller = null)
        {
            return new V1OwnerReference(
                apiVersion: entity.ApiVersion,
                kind: entity.Kind,
                name: entity.Metadata.Name,
                uid: entity.Metadata.Uid,
                blockOwnerDeletion: blockOwnerDeletion,
                controller: controller);
        }
    }
}