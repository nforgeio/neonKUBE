//-----------------------------------------------------------------------------
// FILE:        V1TestDatabase.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright � 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Kube;
using Neon.Operator;
using Neon.Kube.Resources;
using Newtonsoft.Json;

namespace Test.Neon.Kube.Operator
{
    /// <summary>
    /// Used for unit testing Kubernetes clients.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, ApiVersion = KubeApiVersion, Kind = KubeKind, PluralName = KubePlural)]
    [EntityScope(EntityScope.Namespaced)]
    public class V1TestDatabase : IKubernetesObject<V1ObjectMeta>, ISpec<TestDatabaseSpec>, IValidate
    {
        /// <summary>
        /// Object API group.
        /// </summary>
        public const string KubeGroup = "test.neonkube.io";

        /// <summary>
        /// Object API version.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// Object API kind.
        /// </summary>
        public const string KubeKind = "TestDatabase";

        /// <summary>
        /// Object plural name.
        /// </summary>
        public const string KubePlural = "testdatabases";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1TestDatabase()
        {
            ApiVersion = $"{KubeGroup}/{KubeApiVersion}";
            Kind = KubeKind;
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
        /// Gets or sets specification of the desired behavior of the
        /// Tenant.
        /// </summary>
        public TestDatabaseSpec Spec { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }

    /// <summary>
    /// The node execute task specification.
    /// </summary>
    public class TestDatabaseSpec
    {
        /// <summary>
        /// The container image.
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// The number of pods to deploy.
        /// </summary>
        public int Servers { get; set; }

        /// <summary>
        /// The volume size
        /// </summary>
        public string VolumeSize { get; set; }
    }
}
