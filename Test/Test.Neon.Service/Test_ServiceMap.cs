//-----------------------------------------------------------------------------
// FILE:	    Test_ServiceMap.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Service;
using Neon.Xunit;

using Xunit;

namespace TestNeonService
{
    /// <summary>
    /// Demonstrates how two services can be deployed such that one service
    /// uses a <see cref="ServiceMap"/> to lookup up the endpoint of another
    /// service and submit an HTTP request to it.
    /// </summary>
    public class Test_ServiceMap : IClassFixture<ComposedFixture>
    {
        private ServiceMap                          serviceMap;
        private ComposedFixture                     composedFixture;
        private NeonServiceFixture<WebService>      webServiceFixture;
        private NeonServiceFixture<RelayService>    relayServiceFixture;

        public Test_ServiceMap(ComposedFixture fixture)
        {
            this.composedFixture = fixture;
            this.serviceMap      = CreateServiceMap();

            composedFixture.Start(
                () =>
                {
                    composedFixture.AddServiceFixture<WebService>("web-service", new NeonServiceFixture<WebService>(), () => CreateWebService());
                    composedFixture.AddServiceFixture<RelayService>("relay-service", new NeonServiceFixture<RelayService>(), () => CreateRelayService());
                });

            this.webServiceFixture   = (NeonServiceFixture<WebService>)composedFixture["web-service"];
            this.relayServiceFixture = (NeonServiceFixture<RelayService>)composedFixture["relay-service"];

            Assert.True(webServiceFixture.IsRunning);
            Assert.True(relayServiceFixture.IsRunning);
        }

        /// <summary>
        /// Returns the service map.
        /// </summary>
        private ServiceMap CreateServiceMap()
        {
            var serviceMap = new ServiceMap();

            //---------------------------------------------
            // web-service:

            var description = new ServiceDescription()
            {
                Name    = "web-service",
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 666
                });

            serviceMap.Add(description);

            //---------------------------------------------
            // relay-service:

            description = new ServiceDescription()
            {
                Name    = "relay-service",
                Address = "127.0.0.10"
            };

            description.Endpoints.Add(
                new ServiceEndpoint()
                {
                    Protocol   = ServiceEndpointProtocol.Http,
                    PathPrefix = "/",
                    Port       = 777
                });

            serviceMap.Add(description);

            return serviceMap;
        }

        /// <summary>
        /// Creates a <see cref="WebService"/> instance.
        /// </summary>
        /// <returns>The service instance.</returns>
        private WebService CreateWebService()
        {
            var service = new WebService(CreateServiceMap(), "web-service");

            service.SetEnvironmentVariable("WEB_RESULT", "Hello World!");

            return service;
        }

        /// <summary>
        /// Creates a <see cref="RelayService"/> instance.
        /// </summary>
        /// <returns>The service instance.</returns>
        private RelayService CreateRelayService()
        {
            return new RelayService(CreateServiceMap(), "relay-service");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public async Task Test()
        {
            using (var client = new HttpClient())
            {
                // Both the [web-service] and [relay-services] should be running.
                // We'll first query the [web-service] directly and verify that
                // we get the expected response.

                var webServiceDescription = serviceMap["web-service"];

                Assert.Equal("Hello World!", await client.GetStringAsync(webServiceDescription.Endpoints.Default.Uri));

                // Now query the [relay-service] which calls the [web-service]
                // and verify.

                var relayServiceDescription = serviceMap["relay-service"];

                Assert.Equal("Hello World!", await client.GetStringAsync(relayServiceDescription.Endpoints.Default.Uri));
            }
        }
    }
}
