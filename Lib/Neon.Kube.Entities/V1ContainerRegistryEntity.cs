//-----------------------------------------------------------------------------
// FILE:	    V1ContainerRegistryEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

using Neon.Kube;

namespace Neon.Kube.Entities
{
    /// <summary>
    /// Custom resource describing an upstream container registry.
    /// </summary>
    [KubernetesEntity(Group = KubeConst.NeonResourceGroup, ApiVersion = "v1", Kind = "ContainerRegistry", PluralName = "ContainerRegistries")]
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    public class V1ContainerRegistryEntity : CustomKubernetesEntity<V1ContainerRegistryEntity.V1ContainerRegistryEntitySpec, V1ContainerRegistryEntity.V1ContainerRegistryEntityStatus>
    {
        /// <summary>
        /// The container registry specification.
        /// </summary>
        public class V1ContainerRegistryEntitySpec
        {
            /// <summary>
            /// <para>
            /// The target registry's hostname and optional path.  This is required.
            /// </para>
            /// <note>
            /// The hostname may include a leading <b>"*"</b> wildcard character for subdomain matching.
            /// </note>
            /// </summary>
            public string Prefix { get; set; } = null;

            /// <summary>
            /// Indicates that the registry may be accessed via HTTP.  This defaults
            /// to <c>false</c>.
            /// </summary>
            public bool Insecure { get; set; } = false;

            /// <summary>
            /// Indicates that access to the registry is to be blocked.  This defaults
            /// to <c>false</c>.
            /// </summary>
            public bool Blocked { get; set; } = false;

            /// <summary>
            /// Optionally enables registry redirection.  When specified, this indicates that images 
            /// specified to be pulled from <see cref="Prefix"/> will actually be pulled from 
            /// <see cref="Location"/> instead.  This is a nice way to be able to reuse manifests
            /// and Helm charts such that they pull images from an alternate registry without
            /// modification.  This defaults to <c>null</c>.
            /// </summary>
            public string Location { get; set; } = null;

            /// <summary>
            /// Optionally specifies the username used to authenticate with the registry.  
            /// This defaults to <c>null</c>.
            /// </summary>
            public string Username { get; set; } = null;

            /// <summary>
            /// Optionally specifies the password used to authenticate with the registry.  
            /// This defaults to <c>null</c>.
            /// </summary>
            public string Password { get; set; } = null;
        }

        /// <summary>
        /// The container registry status.
        /// </summary>
        public class V1ContainerRegistryEntityStatus
        {
            /// <summary>
            /// Lists the node names that have applied the changes.
            /// </summary>
            public List<string> UpdatedNodes { get; set; } = new List<string>();
        }
    }
}
