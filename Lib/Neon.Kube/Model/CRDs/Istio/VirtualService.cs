﻿//-----------------------------------------------------------------------------
// FILE:	    VirtualService.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// 
    /// </summary>
    [KubernetesEntity(Group = "networking.istio.io", Kind = "VirtualService", ApiVersion = "v1beta1", PluralName = "virtualservices")]
    public class VirtualService : IKubernetesObject<V1ObjectMeta>, ISpec<VirtualServiceSpec>, IValidate
    {
        /// <summary>
        /// Initializes a new instance of the VirtualService class.
        /// </summary>
        public VirtualService()
        {
            ApiVersion = "networking.istio.io/v1beta1";
            Kind = "VirtualService";
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
        /// VirtualService.
        /// </summary>
        [JsonProperty(PropertyName = "spec")]
        public VirtualServiceSpec Spec { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="Microsoft.Rest.ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
        }
    }
}