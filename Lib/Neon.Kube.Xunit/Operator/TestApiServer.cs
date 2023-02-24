// FILE:	    TestApiServer.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Neon.Kube.Xunit.Operator
{
    /// <inheritdoc/>
    public class TestApiServer : ITestApiServer
    {
        /// <inheritdoc/>
        public List<IKubernetesObject<V1ObjectMeta>> Resources { get; } = new List<IKubernetesObject<V1ObjectMeta>>();

        /// <inheritdoc/>
        public Dictionary<string, Type> Types { get; } = new Dictionary<string, Type>();

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="options">Specifies the test API server options.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public TestApiServer(IOptions<TestApiServerOptions> options)
        {
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));
        }

        /// <inheritdoc/>
        public virtual Task UnhandledRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public virtual void AddResource(string group, string version, string plural, object resource)
        {
            Resources.Add((IKubernetesObject<V1ObjectMeta>)resource);
        }

        /// <inheritdoc/>
        public virtual void AddResource<T>(string group, string version, string plural, T resource)
            where T : IKubernetesObject<V1ObjectMeta>
        {
            Resources.Add(resource);
        }
    }
}
