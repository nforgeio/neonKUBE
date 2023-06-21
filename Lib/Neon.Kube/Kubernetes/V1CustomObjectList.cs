//-----------------------------------------------------------------------------
// FILE:        V1CustomObjectList.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds a list of generic custom object instances returned by the API server.
    /// </summary>
    /// <typeparam name="T">The custom object type.</typeparam>
    public class V1CustomObjectList<T> : IKubernetesObject<V1ListMeta>, IKubernetesObject, IMetadata<V1ListMeta>, IItems<T>, IValidate
        where T : IKubernetesObject<V1ObjectMeta>
    {
        /// <inheritdoc/>
        public string ApiVersion { get; set; }

        /// <inheritdoc/>
        public string Kind { get; set; }

        /// <inheritdoc/>
        public V1ListMeta Metadata { get; set; }

        /// <inheritdoc/>
        public IList<T> Items { get; set; }

        /// <inheritdoc/>
        public void Validate()
        {
        }
    }
}
