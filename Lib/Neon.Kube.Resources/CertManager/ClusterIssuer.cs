//-----------------------------------------------------------------------------
// FILE:        ClusterIssuer.cs
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
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// A ClusterIssuer represents a certificate issuing authority which can be referenced as part of issuerRef fields. 
    /// It is similar to an Issuer, however it is cluster-scoped and therefore can be referenced by resources that exist 
    /// in any namespace, not just the same namespace as the referent.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, Kind = KubeKind, ApiVersion = KubeApiVersion, PluralName = KubePlural)]
    public class ClusterIssuer : IKubernetesObject<V1ObjectMeta>, ISpec<V1IssuerSpec>, IValidate
    {
        /// <summary>
        /// The API version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v1";

        /// <summary>
        /// The Kubernetes named schema this object is based on.
        /// </summary>
        public const string KubeKind = "ClusterIssuer";

        /// <summary>
        /// The Group this Kubernetes type belongs to.
        /// </summary>
        public const string KubeGroup = "cert-manager.io";

        /// <summary>
        /// The plural name of the entity.
        /// </summary>
        public const string KubePlural = "clusterissuers";
        /// <summary>
        /// Initializes a new instance of the ClusterIssuer class.
        /// </summary>
        /// 
        public ClusterIssuer()
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
        [JsonProperty(PropertyName = "apiVersion")]
        public string ApiVersion { get; set; }

        /// <summary>
        /// Gets or sets kind is a string value representing the REST resource
        /// this object represents. Servers may infer this from the endpoint
        /// the client submits requests to. Cannot be updated. In CamelCase.
        /// More info:
        /// https://git.k8s.io/community/contributors/devel/sig-architecture/api-conventions.md#types-kinds
        /// </summary>
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets standard object metadata.
        /// </summary>
        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; set; }

        /// <summary>
        /// Gets or sets specification of the desired behavior of the
        /// ClusterIssuer.
        /// </summary>
        [JsonProperty(PropertyName = "spec")]
        public V1IssuerSpec Spec { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
