//-----------------------------------------------------------------------------
// FILE:        TestResourceController.cs
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
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Kube;
using Neon.Operator.Controllers;

namespace Test.Neon.Kube.Operator
{
    public class TestResourceController : ResourceControllerBase<V1TestResource>
    {
        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestResourceController(IKubernetes k8s)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s = k8s;
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1TestResource resource)
        {
            var childResource = new V1TestChildResource();
            childResource.Metadata = new V1ObjectMeta()
            {
                Name = "child-object"
            };
            childResource.Spec = new ChildTestSpec()
            {
                Message = "I'm a child"
            };

            await k8s.CustomObjects.CreateClusterCustomObjectAsync<V1TestChildResource>(childResource, childResource.Name());

            return await Task.FromResult<ResourceControllerResult>(null);
        }
    }
}
