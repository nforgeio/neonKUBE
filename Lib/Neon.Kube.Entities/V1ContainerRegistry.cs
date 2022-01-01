//-----------------------------------------------------------------------------
// FILE:	    V1ContainerRegistry.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Text;

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

using Neon.Kube;

namespace Neon.Kube.Entities
{
    /// <summary>
    /// Describing an upstream container registry to be configured on each of the cluster nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>neon-node-agent</b> static pods running on all clauster nodes monitor the set of 
    /// <see cref="V1ContainerRegistry"/> as well as any referenced secrets for changes and update
    /// the CRI-O configuration to match.
    /// </para>
    /// </remarks>
    [KubernetesEntity(Group = KubeConst.NeonResourceGroup, ApiVersion = "v1", Kind = "containerregistry", PluralName = "containerregistries")]
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Describes a neonKUBE cluster upstream container registry.")]
    public class V1ContainerRegistry : CustomKubernetesEntity<V1ContainerRegistry.V1ContainerRegistryEntitySpec>
    {
        /// <summary>
        /// The container registry specification.
        /// </summary>
        public class V1ContainerRegistryEntitySpec
        {
            private const string prefixRegex = @"^([\*a-zA-Z0-9-_]?\.)+([a-zA-Z0-9-_]+\.)[a-zA-Z0-9-_]+(/[a-zA-Z0-9-\._~\[\]@\!$&'\(\)\*+,;%=]*)*$";

            /// <summary>
            /// <para>
            /// The target registry's hostname and optional path.  This is required.
            /// </para>
            /// <note>
            /// The prefix may include a leading <b>"*"</b> wildcard character for subdomain matching.
            /// </note>
            /// </summary>
            [Required]
            [Pattern(@"^(\*.)?" + prefixRegex)]
            public string Prefix { get; set; } = null;

            /// <summary>
            /// <para>
            /// Optionally indicates that the registry will be searched for image pulls that
            /// don't specify a registry host/prefix.  This is often used to specify Docker Hub
            /// <b>docker.io</b> as the default prefix since many tutotials and Helm charts 
            /// assume this default due to the popularity of Docker.
            /// </para>
            /// <para>
            /// Specify a non-negative number here to enable this.  Registries will be added to
            /// the search list in ascending order by <see cref="SearchOrder"/> and when two
            /// registries have the same order value, in ascending order by <see cref="Prefix"/>
            /// (lowercase).
            /// </para>
            /// </summary>
            public int SearchOrder { get; set; } = -1;

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
            [Pattern(prefixRegex)]
            public string Location { get; set; } = null;

            /// <summary>
            /// <para>
            /// Optionally identifies the <b>kubernetes.io/basic-auth</b> secret used to authenticate 
            /// with the registry.  This is formatted as: <b>NAMESPACE/SECRET-NAME</b>
            /// </para>
            /// <para>
            /// This defaults to <c>null</c>.
            /// </para>
            /// </summary>
            [Pattern(@"^[a-z0-9-\.]+/[a-z0-9-\.]+$")]
            [Length(MaxLength = 253)]
            public string Secret { get; set; } = null;
        }
    }
}
