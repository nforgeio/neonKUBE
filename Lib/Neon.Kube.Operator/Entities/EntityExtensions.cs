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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.JsonDiffPatch;
using System.Text.Json.JsonDiffPatch.Diffs.Formatters;
using System.Text.Json.Nodes;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Entity extension methods.
    /// </summary>
    public static class EntityExtensions
    {
        private static JsonPatchDeltaFormatter JsonPatchDeltaFormatter = new JsonPatchDeltaFormatter();

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

        /// <summary>
        /// Get the CRD name for the entity.
        /// </summary>
        /// <param name="entityType"></param>
        /// <returns></returns>
        public static string GetKubernetesCrdName(this Type entityType)
        {
            var entityMetatdata = entityType.GetKubernetesTypeMetadata();
            return $"{entityMetatdata.PluralName}.{entityMetatdata.Group}";
        }

        /// <summary>
        /// Creates a <see cref="V1Patch"/> given two objects.
        /// </summary>
        /// <param name="oldEntity"></param>
        /// <param name="newEntity"></param>
        /// <returns></returns>
        public static V1Patch CreatePatch(this object oldEntity, object newEntity)
        {
            var node1 = JsonNode.Parse(KubernetesJson.Serialize(oldEntity));
            var node2 = JsonNode.Parse(KubernetesJson.Serialize(newEntity));

            var diff        = node1.Diff(node2, JsonPatchDeltaFormatter);
            var patchString = Convert.ToBase64String(Encoding.UTF8.GetBytes(KubernetesJson.Serialize(diff)));

            return new V1Patch(patchString, V1Patch.PatchType.JsonPatch);
        }
    }
}