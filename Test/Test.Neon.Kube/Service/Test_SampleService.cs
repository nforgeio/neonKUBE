//-----------------------------------------------------------------------------
// FILE:	    Test_SampleService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Service;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    /// <summary>
    /// Demonstrates how to test a <see cref="KubService"/> that has a single HTTP endpoint
    /// and that also exercises environment variable and file based configuration.
    /// </summary>
    public class Test_SampleService : IClassFixture<KubeServiceFixture<SampleService>>
    {
        private KubeServiceFixture<SampleService>   fixture;
        private SampleService                       service;

        public Test_SampleService(KubeServiceFixture<SampleService> fixture)
        {
            fixture.Start(
                () =>
                {
                    return new SampleService(CreateServiceMap(), "sample-service", ThisAssembly.Git.Branch, ThisAssembly.Git.Commit, ThisAssembly.Git.IsDirty);
                });

            this.fixture = fixture;
            this.service = fixture.Service;
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var description = new ServiceDescription()
            {
                Name    = "sample-service",
                Address = IPAddress.Parse("127.0.0.10")
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 666
                });

            var serviceMap = new ServiceMap();

            serviceMap.Add(description);

            return serviceMap;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task Test()
        {
            var client = fixture.GetHttpClient();

            Assert.Equal("Hello World!", await client.GetStringAsync("/"));
        }
    }
}
