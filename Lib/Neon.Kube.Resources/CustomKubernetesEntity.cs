//-----------------------------------------------------------------------------
// FILE:	    CustomKubernetesEntity.cs
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

using Neon.Kube;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Emulates the corresponding <b>KubeOps</b> class for the stand-alone <b>Neon.Kube.Resources</b> library.
    /// </summary>
    public class CustomKubernetesEntity<TSpec, TStatus> : IKubernetesObject<V1ObjectMeta>
        where TSpec : new() 
        where TStatus : new()
    {
        /// <summary>
        /// The resource specification.
        /// </summary>
        public TSpec Spec { get; set; } = new TSpec();

        /// <summary>
        /// The resource status.
        /// </summary>
        public TStatus Status { get; set; } = new TStatus();

        /// <inheritdoc/>
        public string ApiVersion { get; set; }

        /// <inheritdoc/>
        public string Kind { get; set; }

        /// <inheritdoc/>
        public V1ObjectMeta Metadata { get; set; } = new V1ObjectMeta();
    }

    /// <summary>
    /// Emulates the corresponding <b>KubeOps</b> class for the stand-alone <b>Neon.Kube.Resources</b> library.
    /// </summary>
    public class CustomKubernetesEntity<TSpec> : IKubernetesObject<V1ObjectMeta>
        where TSpec : new()
    {
        /// <summary>
        /// The resource specification.
        /// </summary>
        public TSpec Spec { get; set; } = new TSpec();

        /// <inheritdoc/>
        public string ApiVersion { get; set; }

        /// <inheritdoc/>
        public string Kind { get; set; }

        /// <inheritdoc/>
        public V1ObjectMeta Metadata { get; set; } = new V1ObjectMeta();
    }
}
