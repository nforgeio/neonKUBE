//-----------------------------------------------------------------------------
// FILE:        ExternalDnsEndpoint.cs
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
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using k8s;
using k8s.Models;

using Neon.Operator.Attributes;

using Newtonsoft.Json;

// $todo(marcusbooyah):
//
// The generated CRD fails to install in the [neon-cluster-operator] Helm chart
// because it's missing the special [api-approved.kubernetes.io] annotation.
// Here's the Helm error:
//
//      CustomResourceDefinition.apiextensions.k8s.io "dnsendpoints.externaldns.k8s.io" is invalid: metadata.annotations[api-approved.kubernetes.io]: Required value: protected groups must have approval annotation "api-approved.kubernetes.io"
//
// Perhaps the analyzer could identify resources like this by looking for
// [KubeGroup] values ending in ".k8s.io" and add this annotation or not
// generate the CRD at all (if that makes sense).
//
// I'm not entirely sure that just adding the annotation will work.  This post
// describes how doing this can result in unexpected (aka BAD) behavior:
//
//      https://raesene.github.io/blog/2021/11/01/fun-with-CRDs/
//
// The [api-approved.kubernetes.io] annotation doesn't really appear to be
// documented (https://github.com/kubernetes/website/issues/30764).  It's
// supposed to be set the the URL for the Kubernetes GitHub pull request
// that approved the CRD.
//
// It doesn't look like we're referencing this anywhere, so I'm going to
// comment this out for now.

#if TODO

namespace Neon.Kube.Resources.ExternalDns
{
    /// <summary>
    /// ExternalDnsEndpoint.
    /// </summary>
    [KubernetesEntity(Group = KubeGroup, Kind = KubeKind, ApiVersion = KubeApiVersion, PluralName = KubePlural)]
    [Ignore]
    public class ExternalDnsEndpoint : IKubernetesObject<V1ObjectMeta>, ISpec<V1DnsEndpointSpec>, IValidate
    {
        /// <summary>
        /// The API version this Kubernetes type belongs to.
        /// </summary>
        public const string KubeApiVersion = "v1alpha1";

        /// <summary>
        /// The Kubernetes named schema this object is based on.
        /// </summary>
        public const string KubeKind = "DNSEndpoint";

        /// <summary>
        /// The Group this Kubernetes type belongs to.
        /// </summary>
        public const string KubeGroup = "externaldns.k8s.io";

        /// <summary>
        /// The plural name of the entity.
        /// </summary>
        public const string KubePlural = "dnsendpoints";

        /// <summary>
        /// Initializes a new instance of the ExternalDnsEndpoint class.
        /// </summary>
        public ExternalDnsEndpoint()
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
        /// ExternalDnsEndpoint.
        /// </summary>
        [JsonProperty(PropertyName = "spec")]
        public V1DnsEndpointSpec Spec { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

#endif
