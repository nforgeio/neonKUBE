//-----------------------------------------------------------------------------
// FILE:	    ResourceApiController.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Neon.Tasks;

using k8s;
using k8s.Models;

namespace Neon.Kube.Operator.Xunit
{
    /// <summary>
    /// Generic resource API controller.
    /// </summary>
    [Route("api/{version}/{plural}")]
    public class ResourceApiController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly ITestApiServer testApiServer;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="testApiServer"></param>
        public ResourceApiController(ITestApiServer testApiServer)
        {
            this.testApiServer = testApiServer;
        }

        /// <summary>
        /// The custom resource version. <see cref="IKubernetesObject.ApiVersion"/>.
        /// </summary>
        [FromRoute]
        public string Version { get; set; }

        /// <summary>
        /// The plural name of the <see cref="IKubernetesObject"/>.
        /// </summary>
        [FromRoute]
        public string Plural { get; set; }

        /// <summary>
        /// Creates a resource and stores it in <see cref="TestApiServer.Resources"/>
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>An action result containing the resource.</returns>
        [HttpPost]
        public async Task<ActionResult<ResourceObject>> CreateAsync(ResourceObject resource)
        {
            await SyncContext.Clear;

            testApiServer.AddResource(string.Empty, Version, Plural, resource);

            return Ok(resource);
        }
    }
}
