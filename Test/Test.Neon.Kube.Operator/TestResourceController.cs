//-----------------------------------------------------------------------------
// FILE:	    TestResourceController.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Util;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prometheus;
using System.Xml.Linq;

namespace Test.Neon.Kube.Operator
{
    public class TestResourceController : IOperatorController<V1TestResource>
    {
        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public TestResourceController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
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