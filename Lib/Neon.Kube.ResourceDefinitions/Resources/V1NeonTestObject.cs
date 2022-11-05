//-----------------------------------------------------------------------------
// FILE:	    V1NeonTestObject.cs
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
using System.Text;

using k8s;
using k8s.Models;

#if KUBEOPS
using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
using Neon.Kube;
#endif

#if KUBEOPS
namespace Neon.Kube.ResourceDefinitions
#else
namespace Neon.Kube.Resources
#endif
{
    /// <summary>
    /// Used for unit testing Kubernetes clients.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
#if KUBEOPS
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Used internally by the neonKUBE Team for testing purposes.")]
#endif
    public class V1NeonTestObject : CustomKubernetesEntity<V1NeonTestObject.TestSpec, V1NeonTestObject.TestStatus>
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
        public const string KubeKind = "NeonTestObject";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "neontestobjects";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NeonTestObject()
        {
            this.SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class TestSpec
        {
            /// <summary>
            /// A test string.
            /// </summary>
            public string Message { get; set; }
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class TestStatus
        {
            /// <summary>
            /// Testing <see cref="DateTime"/> .
            /// </summary>
            public DateTime? Timestamp { get; set; }
        }
    }
}
