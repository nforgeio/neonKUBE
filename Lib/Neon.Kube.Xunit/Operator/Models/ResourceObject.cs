// FILE:	    ResourceObject.cs
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
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Xunit.Operator
{
    /// <summary>
    /// Generic resource.
    /// </summary>
    public class ResourceObject : IKubernetesObject<V1ObjectMeta>
    {
        /// <inheritdoc/>
        [JsonPropertyName("apiVersion")] 
        public string ApiVersion { get; set; }

        /// <inheritdoc/>
        [JsonPropertyName("kind")]
        public string Kind { get; set; }

        /// <inheritdoc/>
        [JsonPropertyName("metadata")]
        public V1ObjectMeta Metadata { get; set; }

        //[JsonPropertyName("spec")]
        //public T Spec { get; set; }
    }
}
