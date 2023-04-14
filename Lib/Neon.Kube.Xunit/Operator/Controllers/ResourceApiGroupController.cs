//-----------------------------------------------------------------------------
// FILE:	    ResourceApiGroupController.cs
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

using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Neon.Common;
using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Neon.Kube.Xunit.Operator
{
    /// <summary>
    /// Generic resource API controller.
    /// </summary>
    [Route("apis/{group}/{version}/{plural}")]
    [Route("apis/{group}/{version}/{plural}/{name}")]
    [Route("apis/{group}/{version}/namespaces/{namespace}/{plural}")]
    public class ResourceApiGroupController : Microsoft.AspNetCore.Mvc.Controller
    {
        private readonly ITestApiServer testApiServer;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Constructos.
        /// </summary>
        /// <param name="testApiServer"></param>
        /// <param name="jsonSerializerOptions"></param>
        public ResourceApiGroupController(
            ITestApiServer testApiServer,
            JsonSerializerOptions jsonSerializerOptions)
        {
            this.testApiServer = testApiServer;
            this.jsonSerializerOptions = jsonSerializerOptions;
        }

        /// <summary>
        /// The group of the <see cref="IKubernetesObject"/>.
        /// </summary>
        [FromRoute]
        public string Group { get; set; }

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
        /// The namespace name of the <see cref="IKubernetesObject"/>.
        /// </summary>
        [FromRoute]
        public string Namespace { get; set; }

        /// <summary>
        /// The name name of the <see cref="IKubernetesObject"/>.
        /// </summary>
        [FromRoute]
        public string Name { get; set; }

        /// <summary>
        /// Get the list of resources
        /// </summary>
        /// <returns>An action result containing the resources.</returns>
        [HttpGet]
        public async Task<ActionResult> GetAsync()
        {
            await SyncContext.Clear;

            var key = $"{Group}/{Version}/{Plural}";
            if (testApiServer.Types.TryGetValue(key, out Type type))
            {
                var typeMetadata = type.GetKubernetesTypeMetadata();

                if (Name == null)
                {
                    var resources = testApiServer.Resources.Where(r => r.GetType() == type);

                    var CustomObjectListType        = typeof(V1CustomObjectList<>);
                    Type[] typeArgs                 = { type };
                    var customObjectListGenericType = CustomObjectListType.MakeGenericType(typeArgs);
                    dynamic customObjectList        = Activator.CreateInstance(customObjectListGenericType);

                    var iListType        = typeof(IList<>);
                    var iListGenericType = iListType.MakeGenericType(typeArgs);

                    var stringList = NeonHelper.JsonSerialize(resources);
                    var result     = (dynamic)JsonSerializer.Deserialize(stringList, iListGenericType, jsonSerializerOptions);

                    customObjectList.Items = result;

                    return Ok(customObjectList);
                }
                else
                {
                    var resource = testApiServer.Resources.Where(
                        r => r.Kind == typeMetadata.Kind
                        && r.Metadata.Name == Name).FirstOrDefault();

                    if (resource == null)
                    {
                        return NotFound();
                    }

                    return Ok(resource);
                }
            }

            return NotFound();
        }

        /// <summary>
        /// Creates a resource and stores it in <see cref="TestApiServer.Resources"/>
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>An action result containing the resource.</returns>
        [HttpPost]
        public async Task<ActionResult<ResourceObject>> CreateAsync([FromBody] object resource)
        {
            await SyncContext.Clear;

            var key = $"{Group}/{Version}/{Plural}";
            if (testApiServer.Types.TryGetValue(key, out Type type))
            {
                var typeMetadata = type.GetKubernetesTypeMetadata();

                var s = JsonSerializer.Serialize(resource);
                var instance = JsonSerializer.Deserialize(s, type, jsonSerializerOptions);

                testApiServer.AddResource(Group, Version, Plural, instance);

                return Ok(resource);
            }

            return NotFound();
        }

        /// <summary>
        /// Replaces a resource and stores it in <see cref="TestApiServer.Resources"/>
        /// </summary>
        /// <param name="resource"></param>
        /// <returns>An action result containing the resource.</returns>
        [HttpPut]
        public async Task<ActionResult<ResourceObject>> UpdateAsync([FromBody] object resource)
        {
            await SyncContext.Clear;

            var key = $"{Group}/{Version}/{Plural}";
            if (testApiServer.Types.TryGetValue(key, out Type type))
            {
                var typeMetadata = type.GetKubernetesTypeMetadata();

                var s = JsonSerializer.Serialize(resource);
                var instance = JsonSerializer.Deserialize(s, type, jsonSerializerOptions);

                var resources = testApiServer.Resources.Where(
                    r => r.Kind == typeMetadata.Kind
                    && r.Metadata.Name == Name).ToList();

                foreach (var r in resources)
                {
                    testApiServer.Resources.Remove(r);
                }

                testApiServer.AddResource(Group, Version, Plural, instance);

                return Ok(resource);
            }

            return NotFound();
        }

        /// <summary>
        /// Patches a resource in <see cref="TestApiServer.Resources"/>
        /// </summary>
        /// <param name="patchDoc"></param>
        /// <returns>An action result containing the resource.</returns>
        [HttpPatch]
        public async Task<ActionResult<ResourceObject>> PatchAsync(
            [FromBody] JsonPatchDocument<IKubernetesObject<V1ObjectMeta>> patchDoc)
        {
            await SyncContext.Clear;

            var key = $"{Group}/{Version}/{Plural}";
            if (testApiServer.Types.TryGetValue(key, out Type type))
            {
                var typeMetadata = type.GetKubernetesTypeMetadata();

                var resources = testApiServer.Resources.Where(
                    r => r.Kind == typeMetadata.Kind
                    && r.Metadata.Name == Name).ToList();

                var resource = resources.FirstOrDefault();

                if (resource == null)
                {
                    return NotFound();
                }

                patchDoc.ApplyTo(resource);
                
                return Ok(resource);
            }

            return NotFound();
        }
    }
}
