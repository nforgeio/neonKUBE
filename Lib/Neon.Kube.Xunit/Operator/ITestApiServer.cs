// FILE:	    ITestApiServer.cs
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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

using k8s;
using k8s.Models;

namespace Neon.Kube.Xunit.Operator
{
    /// <summary>
    /// Test API server.
    /// </summary>
    public interface ITestApiServer
    {
        /// <summary>
        /// The resources contained by the API server.
        /// </summary>
        List<IKubernetesObject<V1ObjectMeta>> Resources { get; }

        /// <summary>
        /// The types that the API server recognises.
        /// </summary>
        Dictionary<string, Type> Types { get; }

        /// <summary>
        /// No op.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        Task UnhandledRequest(HttpContext context);

        /// <summary>
        /// Adds a resource to the API server's resource collection.
        /// </summary>
        /// <param name="group"></param>
        /// <param name="version"></param>
        /// <param name="plural"></param>
        /// <param name="resource"></param>
        void AddResource(string group, string version, string plural, object resource);

        /// <summary>
        /// Adds a resource to the API server's resource collection.
        /// </summary>
        /// <param name="group"></param>
        /// <param name="version"></param>
        /// <param name="plural"></param>
        /// <param name="resource"></param>
        void AddResource<T>(string group, string version, string plural, T resource)
            where T : IKubernetesObject<V1ObjectMeta>;
    }
}
