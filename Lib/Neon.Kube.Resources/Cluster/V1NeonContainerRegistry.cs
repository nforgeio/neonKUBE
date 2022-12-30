//-----------------------------------------------------------------------------
// FILE:	    V1NeonContainerRegistry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;

using k8s;
using k8s.Models;

using NJsonSchema.Annotations;

namespace Neon.Kube.Resources.Cluster
{
    /// <summary>
    /// Describes an upstream container registry to be configured on each of the cluster nodes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <b>neon-node-agent</b> pods running as a daemonset on all cluster nodes monitor the  
    /// <see cref="V1NeonContainerRegistry"/> resources in the <b>neon-system</b> namespace.
    /// </para>
    /// </remarks>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Cluster)]
    public class V1NeonContainerRegistry : IKubernetesObject<V1ObjectMeta>, ISpec<V1NeonContainerRegistry.RegistrySpec>
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = ResourceHelper.NeonKubeResourceGroup;

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "NeonContainerRegistry";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neoncontainerregistries";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonContainerRegistry()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind       = KubeKind;
        }

        /// <summary>
        /// Gets or sets APIVersion defines the versioned schema of this
        /// representation of an object. Servers should convert recognized
        /// schemas to the latest internal value, and may reject unrecognized
        /// values. More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#resources
        /// </summary>
        public string ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets kind is a string value representing the REST resource
        /// this object represents. Servers may infer this from the endpoint
        /// the client submits requests to. Cannot be updated. In CamelCase.
        /// More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// The spec.
        /// </summary>
        public RegistrySpec Spec { get; set; }

        /// <summary>
        /// The container registry specification.
        /// </summary>
        public class RegistrySpec
        {
            private const string prefixRegex = @"^(\*\.)?([a-zA-Z0-9-_]+\.)*([a-zA-Z0-9-_]+)(/[a-zA-Z0-9-\._~\[\]@\!&'\(\)\*+,;%=\$]+)*$";

            /// <summary>
            /// <para>
            /// The target registry's hostname and optional path.  This is required.
            /// </para>
            /// <note>
            /// The prefix may include a leading <b>"*"</b> wildcard character for subdomain matching.
            /// </note>
            /// </summary>
            [Required]
            [RegularExpression(@"^(\*.)?" + prefixRegex)]
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
            [RegularExpression(prefixRegex)]
            public string Location { get; set; } = null;

            /// <summary>
            /// Optionally specifies the username to be used to authenticate against the upstream registry.
            /// </summary>
            public string Username { get; set; } = null;

            /// <summary>
            /// Optionally specifies the password to be used to authenticate against the upstream registry.
            /// </summary>
            public string Password { get; set; } = null;
        }
    }
}
